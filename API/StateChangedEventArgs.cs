namespace ImageSearchCL.API;

/// <summary>
/// Event arguments for tracking session state transitions.
/// </summary>
/// <remarks>
/// Raised when a tracking session transitions between states:
/// NotStarted → Running → Paused → Running → Stopped → Disposed
///
/// Use Cases:
/// - UI updates (enabling/disabling controls based on tracking state)
/// - Logging and diagnostics (tracking session lifecycle)
/// - Resource management (cleanup on Stopped/Disposed)
/// - Error handling (detecting unexpected state transitions)
/// </remarks>
public class StateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous state before the transition.
    /// </summary>
    /// <value>
    /// The tracking state before the change occurred.
    /// </value>
    public TrackingState OldState { get; }

    /// <summary>
    /// Gets the new state after the transition.
    /// </summary>
    /// <value>
    /// The tracking state after the change occurred.
    /// </value>
    public TrackingState NewState { get; }

    /// <summary>
    /// Gets the timestamp when the state transition occurred.
    /// </summary>
    /// <value>
    /// UTC timestamp of the state change.
    /// </value>
    /// <remarks>
    /// Used for diagnostics, logging, and measuring session durations.
    /// </remarks>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="oldState">The previous tracking state.</param>
    /// <param name="newState">The new tracking state.</param>
    /// <param name="timestamp">The timestamp of the state transition.</param>
    public StateChangedEventArgs(TrackingState oldState, TrackingState newState, DateTime timestamp)
    {
        OldState = oldState;
        NewState = newState;
        Timestamp = timestamp;
    }
}
