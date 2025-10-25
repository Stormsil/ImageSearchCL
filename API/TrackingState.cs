namespace ImageSearchCL.API;

/// <summary>
/// Represents the lifecycle state of a tracking session.
/// </summary>
public enum TrackingState
{
    /// <summary>
    /// Tracking session has been created but not yet started.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Tracking session is actively processing frames and emitting events.
    /// </summary>
    Running,

    /// <summary>
    /// Tracking session is temporarily paused (no frame processing, no events).
    /// </summary>
    Paused,

    /// <summary>
    /// Tracking session has been permanently stopped (cannot be restarted).
    /// </summary>
    Stopped,

    /// <summary>
    /// Tracking session has been disposed and all resources released.
    /// </summary>
    Disposed
}
