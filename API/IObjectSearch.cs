namespace ImageSearchCL.API;

/// <summary>
/// Represents a real-time object tracking session.
/// </summary>
/// <remarks>
/// This is the main public API for ImageSearchCL. It provides:
/// - Event-driven notifications (Appeared, Disappeared, Moved, StateChanged)
/// - Lifecycle management (Start, Pause, Resume, Stop)
/// - State inspection (State, LastResult)
/// - Configuration access (Configuration)
///
/// Lifecycle:
/// 1. Create instance with IObjectSearchFactory.CreateSession()
/// 2. Subscribe to events (Appeared, Disappeared, Moved, StateChanged)
/// 3. Call Start() to begin tracking
/// 4. Receive events on SynchronizationContext thread (UI-safe)
/// 5. Call Pause() / Resume() to control tracking
/// 6. Call Stop() or Dispose() to end tracking
///
/// Thread Safety:
/// - All events are marshalled to the SynchronizationContext captured at construction
/// - Properties are thread-safe for reading
/// - Methods are thread-safe (can be called from any thread)
///
/// Performance Expectations:
/// - Frame processing: ≥30 FPS (≤33ms per frame)
/// - Detection latency: &lt;50ms from frame capture to event emission
/// - CPU usage: &lt;5% when object is stationary (idle state)
/// - Memory overhead: &lt;1KB per tracking session
/// </remarks>
public interface IObjectSearch : IDisposable
{
    #region Events

    /// <summary>
    /// Raised when the tracked object appears on screen.
    /// </summary>
    /// <remarks>
    /// Event Semantics:
    /// - Emitted when object transitions from NotVisible → Visible
    /// - Confidence must be ≥ configured threshold (default: 0.8)
    /// - Event args contain the FindResult with object position and anchors
    /// - Event is marshalled to the SynchronizationContext (UI-safe)
    ///
    /// Typical Latency:
    /// - 30-50ms from object appearing to event emission
    /// - Depends on frame rate and template matching performance
    ///
    /// Use Cases:
    /// - UI automation (clicking/interacting with newly visible elements)
    /// - Screen recording markers (highlighting when object appears)
    /// - Logging and analytics (tracking object visibility patterns)
    /// </remarks>
    event EventHandler<FindResult> Appeared;

    /// <summary>
    /// Raised when the tracked object disappears from screen.
    /// </summary>
    /// <remarks>
    /// Event Semantics:
    /// - Emitted when object transitions from Visible → NotVisible
    /// - Happens when confidence drops below threshold or object moves off-screen
    /// - Event args contain the last known FindResult before disappearance
    /// - Event is marshalled to the SynchronizationContext (UI-safe)
    ///
    /// Typical Latency:
    /// - 30-50ms from object disappearing to event emission
    ///
    /// Use Cases:
    /// - UI automation (detecting when elements are hidden/closed)
    /// - Error detection (alerting when expected element disappears)
    /// - State management (updating application state on object disappearance)
    /// </remarks>
    event EventHandler<FindResult> Disappeared;

    /// <summary>
    /// Raised when the tracked object moves beyond the configured threshold.
    /// </summary>
    /// <remarks>
    /// Event Semantics:
    /// - Emitted when object center moves ≥ MovementThreshold pixels (default: 5px)
    /// - Event args contain both old and new FindResult plus distance traveled
    /// - Event is marshalled to the SynchronizationContext (UI-safe)
    /// - Not emitted during Appeared/Disappeared transitions
    ///
    /// Movement Detection:
    /// - Distance measured between old and new center points (Euclidean)
    /// - Threshold prevents jitter from minor template matching variations
    /// - High threshold (e.g., 20px) for coarse tracking, low (e.g., 2px) for precise
    ///
    /// Typical Latency:
    /// - 30-50ms from object moving to event emission
    ///
    /// Use Cases:
    /// - UI automation (tracking moving elements for interaction)
    /// - Motion analysis (calculating velocity, trajectory)
    /// - Visual debugging (drawing trails showing object movement)
    /// </remarks>
    event EventHandler<MovedEventArgs> Moved;

    /// <summary>
    /// Raised when the tracking session state changes.
    /// </summary>
    /// <remarks>
    /// Event Semantics:
    /// - Emitted on all state transitions (NotStarted → Running → Paused → Stopped → Disposed)
    /// - Event args contain old state, new state, and transition timestamp
    /// - Event is marshalled to the SynchronizationContext (UI-safe)
    ///
    /// State Transitions:
    /// - NotStarted → Running: Start() called
    /// - Running → Paused: Pause() called
    /// - Paused → Running: Resume() called
    /// - Running/Paused → Stopped: Stop() called
    /// - Any → Disposed: Dispose() called
    ///
    /// Use Cases:
    /// - UI updates (enabling/disabling controls based on state)
    /// - Logging and diagnostics (tracking session lifecycle)
    /// - Resource management (cleanup on Stopped/Disposed)
    /// </remarks>
    event EventHandler<StateChangedEventArgs> StateChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current state of the tracking session.
    /// </summary>
    /// <value>
    /// Current lifecycle state (NotStarted, Running, Paused, Stopped, Disposed).
    /// </value>
    /// <remarks>
    /// Thread-safe: Can be read from any thread.
    /// </remarks>
    TrackingState State { get; }

    /// <summary>
    /// Gets the most recent detection result, or null if object is not visible.
    /// </summary>
    /// <value>
    /// FindResult from the most recent detection, or null if object is NotVisible.
    /// </value>
    /// <remarks>
    /// Thread-safe: Can be read from any thread.
    /// Updated on every frame where object is detected.
    /// Useful for polling scenarios or immediate position queries.
    /// </remarks>
    FindResult? LastResult { get; }

    /// <summary>
    /// Gets the configuration for this tracking session.
    /// </summary>
    /// <value>
    /// Immutable configuration containing thresholds, reference image, and settings.
    /// </value>
    /// <remarks>
    /// Configuration is immutable and set at construction time.
    /// To change configuration, create a new tracking session.
    /// </remarks>
    TrackingConfiguration Configuration { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Starts the tracking session and begins emitting events.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if session is not in NotStarted state.
    /// </exception>
    /// <remarks>
    /// After calling Start():
    /// - State transitions to Running
    /// - Frame processing begins
    /// - Events (Appeared, Moved, Disappeared) begin emitting
    ///
    /// Thread-safe: Can be called from any thread.
    /// Non-blocking: Returns immediately (frame processing happens on background thread).
    /// </remarks>
    void Start();

    /// <summary>
    /// Pauses the tracking session temporarily.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if session is not in Running state.
    /// </exception>
    /// <remarks>
    /// After calling Pause():
    /// - State transitions to Paused
    /// - Frame processing stops
    /// - No events are emitted
    /// - LastResult remains unchanged
    ///
    /// Thread-safe: Can be called from any thread.
    /// Non-blocking: Returns immediately.
    /// Can be resumed with Resume().
    /// </remarks>
    void Pause();

    /// <summary>
    /// Resumes a paused tracking session.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if session is not in Paused state.
    /// </exception>
    /// <remarks>
    /// After calling Resume():
    /// - State transitions back to Running
    /// - Frame processing resumes
    /// - Events begin emitting again
    ///
    /// Thread-safe: Can be called from any thread.
    /// Non-blocking: Returns immediately.
    /// </remarks>
    void Resume();

    /// <summary>
    /// Permanently stops the tracking session.
    /// </summary>
    /// <remarks>
    /// After calling Stop():
    /// - State transitions to Stopped
    /// - Frame processing stops permanently
    /// - No more events are emitted
    /// - Resources are released (except configuration/last result)
    ///
    /// Thread-safe: Can be called from any thread.
    /// Non-blocking: Returns immediately.
    /// Idempotent: Safe to call multiple times.
    /// Cannot be restarted (create new session instead).
    /// </remarks>
    void Stop();

    /// <summary>
    /// Blocks until the tracked object becomes visible or timeout expires.
    /// </summary>
    /// <param name="timeout">
    /// Maximum time to wait. Use Timeout.InfiniteTimeSpan to wait indefinitely.
    /// </param>
    /// <returns>
    /// FindResult if object became visible, or null if timeout expired.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if session is not in Running state.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if session has been disposed.
    /// </exception>
    /// <remarks>
    /// Behavior:
    /// - If object is already visible: Returns immediately with current FindResult
    /// - If object is not visible: Blocks until Appeared event fires or timeout
    /// - If timeout expires: Returns null
    /// - If session is stopped while waiting: Returns null
    ///
    /// Thread-safe: Can be called from any thread.
    /// Blocking: Suspends calling thread until condition met or timeout.
    ///
    /// Use Cases:
    /// - Sequential automation scripts waiting for UI elements
    /// - Synchronization points in test scenarios
    /// - Polling alternative (more efficient than spin-wait)
    ///
    /// Example:
    /// <code>
    /// session.Start();
    /// var result = session.WaitUntilVisible(TimeSpan.FromSeconds(5));
    /// if (result != null)
    ///     Console.WriteLine($"Object found at {result.Center}");
    /// else
    ///     Console.WriteLine("Object not found within 5 seconds");
    /// </code>
    /// </remarks>
    FindResult? WaitUntilVisible(TimeSpan timeout);

    /// <summary>
    /// Blocks until the tracked object becomes not visible or timeout expires.
    /// </summary>
    /// <param name="timeout">
    /// Maximum time to wait. Use Timeout.InfiniteTimeSpan to wait indefinitely.
    /// </param>
    /// <returns>
    /// True if object became not visible, false if timeout expired.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if session is not in Running state.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if session has been disposed.
    /// </exception>
    /// <remarks>
    /// Behavior:
    /// - If object is already not visible: Returns immediately with true
    /// - If object is visible: Blocks until Disappeared event fires or timeout
    /// - If timeout expires: Returns false
    /// - If session is stopped while waiting: Returns true
    ///
    /// Thread-safe: Can be called from any thread.
    /// Blocking: Suspends calling thread until condition met or timeout.
    ///
    /// Use Cases:
    /// - Waiting for UI elements to close/disappear
    /// - Synchronization after triggering actions
    /// - Test assertions (verify element removed)
    ///
    /// Example:
    /// <code>
    /// session.Start();
    /// ClickCloseButton();
    /// if (session.WaitUntilNotVisible(TimeSpan.FromSeconds(2)))
    ///     Console.WriteLine("Element closed successfully");
    /// else
    ///     Console.WriteLine("Element still visible after 2 seconds");
    /// </code>
    /// </remarks>
    bool WaitUntilNotVisible(TimeSpan timeout);

    #endregion
}
