using System.Diagnostics;
using System.Drawing;
using ImageSearchCL.API;
using ImageSearchCL.Infrastructure;

namespace ImageSearchCL.Core;

/// <summary>
/// Core implementation of the IObjectSearch tracking session.
/// </summary>
/// <remarks>
/// Architecture Role:
/// - Core layer (business logic, state management, event orchestration)
/// - Implements IObjectSearch public API
/// - Uses TemplateMatchingEngine (Infrastructure) for detection
/// - Uses FrameQueue (Infrastructure) for lock-free frame buffering
/// - Uses ICaptureSession (API) for frame input
///
/// Responsibilities:
/// - State machine management (NotStarted → Running → Paused → Stopped → Disposed)
/// - Frame processing pipeline (capture → queue → detect → compare → emit events)
/// - Event marshalling to SynchronizationContext (UI-safe)
/// - Movement detection and filtering (MovementThreshold)
/// - Visibility state tracking (NotVisible ↔ Visible)
///
/// Threading Model:
/// - Capture thread: ICaptureSession.FrameReady enqueues frames to FrameQueue (lock-free, fast)
/// - Processing thread: Background Task dequeues and processes frames asynchronously
/// - Events marshalled to SynchronizationContext captured at construction
/// - State changes are thread-safe (lock-based)
/// - Properties are thread-safe for reading
///
/// Performance:
/// - Target: ≥30 FPS frame processing (≤33ms per frame)
/// - Latency: &lt;50ms from frame capture to event emission
/// - CPU: &lt;5% when object is stationary (idle optimization)
/// - Memory: &lt;1KB overhead per session (excluding frame buffers)
/// - Frame dropping: Automatic when processing falls behind (single-slot buffer)
/// </remarks>
internal sealed class TrackingSession : IObjectSearch
{
    private static int _activeSessionCount = 0;
    private static readonly object _activeSessionLock = new object();

    private readonly ICaptureSession _captureSession;
    private readonly TrackingConfiguration _configuration;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly object _stateLock = new object();
    private readonly ManualResetEventSlim _visibleEvent = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim _notVisibleEvent = new ManualResetEventSlim(true);
    private readonly FrameQueue _frameQueue = new FrameQueue();

    private TrackingState _state;
    private ObjectVisibility _visibility;
    private FindResult? _lastResult;
    private bool _disposed;
    private Task? _processingTask;
    private CancellationTokenSource? _processingCts;

    /// <inheritdoc/>
    public event EventHandler<FindResult>? Appeared;

    /// <inheritdoc/>
    public event EventHandler<FindResult>? Disappeared;

    /// <inheritdoc/>
    public event EventHandler<MovedEventArgs>? Moved;

    /// <inheritdoc/>
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <inheritdoc/>
    public TrackingState State
    {
        get
        {
            lock (_stateLock)
                return _state;
        }
    }

    /// <inheritdoc/>
    public FindResult? LastResult
    {
        get
        {
            lock (_stateLock)
                return _lastResult;
        }
    }

    /// <inheritdoc/>
    public TrackingConfiguration Configuration => _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingSession"/> class.
    /// </summary>
    /// <param name="captureSession">The capture session providing video frames.</param>
    /// <param name="configuration">The tracking configuration.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="captureSession"/> or <paramref name="configuration"/> is null.
    /// </exception>
    public TrackingSession(ICaptureSession captureSession, TrackingConfiguration configuration)
    {
        _captureSession = captureSession ?? throw new ArgumentNullException(nameof(captureSession));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Capture SynchronizationContext for event marshalling (UI-safe)
        _synchronizationContext = SynchronizationContext.Current;

        _state = TrackingState.NotStarted;
        _visibility = ObjectVisibility.NotVisible;
        _lastResult = null;

        // Subscribe to frame events
        _captureSession.FrameReady += OnFrameReady;
    }

    /// <inheritdoc/>
    public void Start()
    {
        lock (_stateLock)
        {
            if (_state != TrackingState.NotStarted)
                throw new InvalidOperationException($"Cannot start tracking session in state {_state}. Expected NotStarted.");

            ChangeState(TrackingState.Running);

            // Start frame processing task
            _processingCts = new CancellationTokenSource();
            _processingTask = Task.Run(() => ProcessingLoop(_processingCts.Token), _processingCts.Token);

            _captureSession.Start();

            // Show debug overlay if this is the first active session
            if (API.ImageSearchConfiguration.EnableDebugOverlay)
            {
                lock (_activeSessionLock)
                {
                    _activeSessionCount++;
                    if (_activeSessionCount == 1)
                    {
                        try
                        {
                            Infrastructure.DebugOverlay.Instance.Show();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"[TrackingSession] Error showing debug overlay: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Pause()
    {
        lock (_stateLock)
        {
            if (_state != TrackingState.Running)
                throw new InvalidOperationException($"Cannot pause tracking session in state {_state}. Expected Running.");

            ChangeState(TrackingState.Paused);
        }
    }

    /// <inheritdoc/>
    public void Resume()
    {
        lock (_stateLock)
        {
            if (_state != TrackingState.Paused)
                throw new InvalidOperationException($"Cannot resume tracking session in state {_state}. Expected Paused.");

            ChangeState(TrackingState.Running);
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_stateLock)
        {
            if (_state == TrackingState.Stopped || _state == TrackingState.Disposed)
                return; // Already stopped, idempotent

            ChangeState(TrackingState.Stopped);
            _captureSession.Stop();

            // Stop processing task
            _processingCts?.Cancel();
        }

        // Wait for processing task to finish (outside lock)
        try
        {
            _processingTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Ignore cancellation exceptions
        }

        lock (_stateLock)
        {
            _processingCts?.Dispose();
            _processingCts = null;
            _processingTask = null;

            // Clear any buffered frames
            _frameQueue.Clear();

            // Hide debug overlay if this was the last active session
            if (API.ImageSearchConfiguration.EnableDebugOverlay)
            {
                lock (_activeSessionLock)
                {
                    _activeSessionCount--;
                    if (_activeSessionCount == 0)
                    {
                        try
                        {
                            Infrastructure.DebugOverlay.Instance.Hide();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"[TrackingSession] Error hiding debug overlay: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public FindResult? WaitUntilVisible(TimeSpan timeout)
    {
        lock (_stateLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrackingSession));
            if (_state != TrackingState.Running)
                throw new InvalidOperationException($"Cannot wait in state {_state}. Session must be Running.");

            // If already visible, return immediately
            if (_visibility == ObjectVisibility.Visible && _lastResult != null)
                return _lastResult;
        }

        // Wait for visible event (outside lock to avoid deadlock)
        bool signaled = _visibleEvent.Wait(timeout);

        lock (_stateLock)
        {
            // Return result if visible, otherwise null (timeout or stopped)
            return signaled && _visibility == ObjectVisibility.Visible ? _lastResult : null;
        }
    }

    /// <inheritdoc/>
    public bool WaitUntilNotVisible(TimeSpan timeout)
    {
        lock (_stateLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TrackingSession));
            if (_state != TrackingState.Running)
                throw new InvalidOperationException($"Cannot wait in state {_state}. Session must be Running.");

            // If already not visible, return immediately
            if (_visibility == ObjectVisibility.NotVisible)
                return true;
        }

        // Wait for not visible event (outside lock to avoid deadlock)
        bool signaled = _notVisibleEvent.Wait(timeout);

        lock (_stateLock)
        {
            // Return true if not visible or stopped, false if timeout
            return signaled || _state != TrackingState.Running;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
                return; // Already disposed, idempotent

            ChangeState(TrackingState.Disposed);

            _captureSession.FrameReady -= OnFrameReady;
            _captureSession.Dispose();
            _configuration.ReferenceImage.Dispose();

            // Stop processing task
            _processingCts?.Cancel();

            // Signal wait events to unblock any waiting threads
            _visibleEvent.Set();
            _notVisibleEvent.Set();
            _visibleEvent.Dispose();
            _notVisibleEvent.Dispose();

            _disposed = true;
        }

        // Wait for processing task (outside lock)
        try
        {
            _processingTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Ignore cancellation exceptions
        }

        _processingCts?.Dispose();
        _frameQueue.Dispose();

        // Hide debug overlay if this was the last active session
        if (API.ImageSearchConfiguration.EnableDebugOverlay)
        {
            lock (_activeSessionLock)
            {
                _activeSessionCount--;
                if (_activeSessionCount == 0)
                {
                    try
                    {
                        Infrastructure.DebugOverlay.Instance.Hide();
                    }
                    catch
                    {
                        // Ignore overlay errors
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles incoming frames from the capture session.
    /// </summary>
    /// <remarks>
    /// Called on capture thread (ICaptureSession.FrameReady).
    /// Quickly enqueues frame to FrameQueue (lock-free, O(1)).
    /// Frame ownership transferred to queue - do NOT dispose here.
    /// </remarks>
    private void OnFrameReady(object? sender, FrameReadyEventArgs e)
    {
        try
        {
            // Enqueue frame to processing queue (lock-free, fast)
            // Old frame automatically disposed if queue is full
            _frameQueue.Enqueue(e.Frame);
        }
        catch (ObjectDisposedException)
        {
            // Queue disposed, session is shutting down
            e.Frame?.Dispose();
        }
    }

    /// <summary>
    /// Background processing loop that dequeues and processes frames.
    /// </summary>
    /// <remarks>
    /// Runs on background Task thread.
    /// Dequeues frames from FrameQueue and performs template matching.
    /// Automatically skips frames when processing falls behind capture rate.
    /// </remarks>
    private async Task ProcessingLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Dequeue frame from queue (returns null if empty)
                var frame = _frameQueue.Dequeue();

                if (frame == null)
                {
                    // No frame available, wait a bit before checking again
                    await Task.Delay(1, cancellationToken);
                    continue;
                }

                try
                {
                    // Check if tracking is active
                    TrackingState currentState;
                    lock (_stateLock)
                    {
                        currentState = _state;
                    }

                    if (currentState != TrackingState.Running)
                    {
                        // Paused or stopped, skip frame
                        frame.Dispose();
                        continue;
                    }

                    // Perform template matching - try all templates and use best match
                    Infrastructure.TemplateMatchingEngine.MatchResult? matchResult = null;
                    ReferenceImage? matchedTemplate = null;
                    int matchedTemplateIndex = -1;

                    for (int i = 0; i < _configuration.ReferenceImages.Length; i++)
                    {
                        var template = _configuration.ReferenceImages[i];
                        var result = TemplateMatchingEngine.FindTemplate(
                            frame,
                            template.Image!,
                            _configuration.ConfidenceThreshold
                        );

                        // Keep best match (highest confidence)
                        if (result != null && (matchResult == null || result.Confidence > matchResult.Confidence))
                        {
                            matchResult = result;
                            matchedTemplate = template;
                            matchedTemplateIndex = i;
                        }
                    }

                    // Process detection result (best match or null)
                    ProcessDetection(matchResult, matchedTemplate, matchedTemplateIndex, DateTime.UtcNow);
                }
                finally
                {
                    // Always dispose frame after processing
                    frame.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (ObjectDisposedException)
            {
                // Session disposed
                break;
            }
        }
    }

    /// <summary>
    /// Processes a detection result and updates state/emits events.
    /// </summary>
    private void ProcessDetection(
        TemplateMatchingEngine.MatchResult? matchResult,
        ReferenceImage? matchedTemplate,
        int matchedTemplateIndex,
        DateTime timestamp)
    {
        lock (_stateLock)
        {
            if (matchResult == null)
            {
                // Object not visible
                HandleNotVisible();

                // Clear overlay immediately when object disappears
                if (API.ImageSearchConfiguration.EnableDebugOverlay)
                {
                    try
                    {
                        Infrastructure.DebugOverlay.Instance.Clear();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[TrackingSession] Error clearing debug overlay: {ex.Message}");
                    }
                }
            }
            else
            {
                // Object visible - create FindResult or MultiFindResult
                FindResult findResult;

                // Use MultiFindResult if multiple templates are being tracked
                if (_configuration.ReferenceImages.Length > 1 && matchedTemplate != null)
                {
                    findResult = new MultiFindResult(
                        matchResult.X,
                        matchResult.Y,
                        matchResult.Width,
                        matchResult.Height,
                        matchResult.Confidence,
                        timestamp,
                        matchedTemplate,
                        matchedTemplateIndex
                    );
                }
                else
                {
                    findResult = new FindResult(
                        matchResult.X,
                        matchResult.Y,
                        matchResult.Width,
                        matchResult.Height,
                        matchResult.Confidence,
                        timestamp
                    );
                }

                HandleVisible(findResult);
            }
        }

        // Register detection with debug overlay if enabled
        if (API.ImageSearchConfiguration.EnableDebugOverlay && matchResult != null)
        {
            try
            {
                FindResult result;

                // Use MultiFindResult if multiple templates are being tracked
                if (_configuration.ReferenceImages.Length > 1 && matchedTemplate != null)
                {
                    result = new MultiFindResult(
                        matchResult.X,
                        matchResult.Y,
                        matchResult.Width,
                        matchResult.Height,
                        matchResult.Confidence,
                        timestamp,
                        matchedTemplate,
                        matchedTemplateIndex
                    );
                }
                else
                {
                    result = new FindResult(
                        matchResult.X,
                        matchResult.Y,
                        matchResult.Width,
                        matchResult.Height,
                        matchResult.Confidence,
                        timestamp
                    );
                }

                Infrastructure.DebugOverlay.Instance.RegisterDetection(
                    result,
                    API.ImageSearchConfiguration.DebugOverlayColor,
                    API.ImageSearchConfiguration.DebugOverlayThickness,
                    API.ImageSearchConfiguration.DebugOverlayWindowHandle);
            }
            catch
            {
                // Ignore overlay errors - don't disrupt tracking
            }
        }
    }

    /// <summary>
    /// Handles the case where object is not visible (below confidence threshold).
    /// </summary>
    private void HandleNotVisible()
    {
        if (_visibility == ObjectVisibility.Visible)
        {
            // Transition: Visible → NotVisible (Disappeared event)
            var lastResult = _lastResult!; // Must be non-null if visibility was Visible
            _visibility = ObjectVisibility.NotVisible;
            _lastResult = null;

            // Signal WaitUntilNotVisible
            _visibleEvent.Reset();
            _notVisibleEvent.Set();

            RaiseEvent(Disappeared, lastResult);
        }
        // else: Already NotVisible, no event
    }

    /// <summary>
    /// Handles the case where object is visible (above confidence threshold).
    /// </summary>
    private void HandleVisible(FindResult newResult)
    {
        if (_visibility == ObjectVisibility.NotVisible)
        {
            // Transition: NotVisible → Visible (Appeared event)
            _visibility = ObjectVisibility.Visible;
            _lastResult = newResult;

            // Signal WaitUntilVisible
            _notVisibleEvent.Reset();
            _visibleEvent.Set();

            RaiseEvent(Appeared, newResult);
        }
        else
        {
            // Already Visible - check for movement
            var oldResult = _lastResult!; // Must be non-null if visibility is Visible
            double distance = oldResult.DistanceTo(newResult);

            if (distance >= _configuration.MovementThreshold)
            {
                // Object moved beyond threshold (Moved event)
                _lastResult = newResult;

                var movedArgs = new MovedEventArgs(oldResult, newResult);
                RaiseEvent(Moved, movedArgs);
            }
            else
            {
                // Object stationary or movement below threshold - update LastResult silently
                _lastResult = newResult;
            }
        }
    }

    /// <summary>
    /// Changes the tracking state and raises StateChanged event.
    /// </summary>
    /// <remarks>
    /// Must be called under _stateLock.
    /// </remarks>
    private void ChangeState(TrackingState newState)
    {
        var oldState = _state;
        _state = newState;

        var args = new StateChangedEventArgs(oldState, newState, DateTime.UtcNow);
        RaiseEvent(StateChanged, args);
    }

    /// <summary>
    /// Raises an event on the SynchronizationContext (UI-safe).
    /// </summary>
    private void RaiseEvent<T>(EventHandler<T>? eventHandler, T args)
    {
        if (eventHandler == null)
            return;

        if (_synchronizationContext != null)
        {
            // Marshal to UI thread
            _synchronizationContext.Post(_ => eventHandler.Invoke(this, args), null);
        }
        else
        {
            // No SynchronizationContext (console app, background thread, etc.)
            eventHandler.Invoke(this, args);
        }
    }
}
