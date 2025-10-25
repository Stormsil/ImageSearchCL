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
/// - Uses ICaptureSession (API) for frame input
///
/// Responsibilities:
/// - State machine management (NotStarted → Running → Paused → Stopped → Disposed)
/// - Frame processing pipeline (capture → detect → compare → emit events)
/// - Event marshalling to SynchronizationContext (UI-safe)
/// - Movement detection and filtering (MovementThreshold)
/// - Visibility state tracking (NotVisible ↔ Visible)
///
/// Threading Model:
/// - Frame processing happens on background thread (ICaptureSession.FrameReady)
/// - Events marshalled to SynchronizationContext captured at construction
/// - State changes are thread-safe (lock-based)
/// - Properties are thread-safe for reading
///
/// Performance:
/// - Target: ≥30 FPS frame processing (≤33ms per frame)
/// - Latency: &lt;50ms from frame capture to event emission
/// - CPU: &lt;5% when object is stationary (idle optimization)
/// - Memory: &lt;1KB overhead per session (excluding frame buffers)
/// </remarks>
internal sealed class TrackingSession : IObjectSearch
{
    private readonly ICaptureSession _captureSession;
    private readonly TrackingConfiguration _configuration;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly object _stateLock = new object();
    private readonly ManualResetEventSlim _visibleEvent = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim _notVisibleEvent = new ManualResetEventSlim(true);

    private TrackingState _state;
    private ObjectVisibility _visibility;
    private FindResult? _lastResult;
    private bool _disposed;

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
            _captureSession.Start();
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

            // Signal wait events to unblock any waiting threads
            _visibleEvent.Set();
            _notVisibleEvent.Set();
            _visibleEvent.Dispose();
            _notVisibleEvent.Dispose();

            _disposed = true;
        }
    }

    /// <summary>
    /// Handles incoming frames from the capture session.
    /// </summary>
    /// <remarks>
    /// Called on background thread (ICaptureSession.FrameReady).
    /// Processing pipeline:
    /// 1. Check if tracking is active (Running state)
    /// 2. Perform template matching
    /// 3. Update visibility state
    /// 4. Detect movement
    /// 5. Emit events (marshalled to SynchronizationContext)
    /// 6. Dispose frame bitmap
    /// </remarks>
    private void OnFrameReady(object? sender, FrameReadyEventArgs e)
    {
        try
        {
            // Check if tracking is active
            TrackingState currentState;
            lock (_stateLock)
            {
                currentState = _state;
            }

            if (currentState != TrackingState.Running)
                return; // Paused or stopped, skip processing

            // Clone frame to avoid concurrent access in OpenCvSharp
            Bitmap frameClone;
            lock (e.Frame)
            {
                frameClone = (Bitmap)e.Frame.Clone();
            }

            // Perform template matching
            var matchResult = TemplateMatchingEngine.FindTemplate(
                frameClone,
                _configuration.ReferenceImage.Image!,
                _configuration.ConfidenceThreshold
            );

            frameClone.Dispose();

            // Process detection result
            ProcessDetection(matchResult, e.Timestamp);
        }
        finally
        {
            // Always dispose frame (ownership transferred from ICaptureSession)
            e.Frame?.Dispose();
        }
    }

    /// <summary>
    /// Processes a detection result and updates state/emits events.
    /// </summary>
    private void ProcessDetection(TemplateMatchingEngine.MatchResult? matchResult, DateTime timestamp)
    {
        lock (_stateLock)
        {
            if (matchResult == null)
            {
                // Object not visible
                HandleNotVisible();
            }
            else
            {
                // Object visible - create FindResult
                var findResult = new FindResult(
                    matchResult.X,
                    matchResult.Y,
                    matchResult.Width,
                    matchResult.Height,
                    matchResult.Confidence,
                    timestamp
                );

                HandleVisible(findResult);
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
