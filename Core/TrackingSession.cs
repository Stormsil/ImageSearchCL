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
internal sealed partial class TrackingSession : IObjectSearch
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

}
