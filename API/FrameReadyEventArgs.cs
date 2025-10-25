using System.Drawing;

namespace ImageSearchCL.API;

/// <summary>
/// Event arguments for the <see cref="ICaptureSession.FrameReady"/> event.
/// </summary>
public class FrameReadyEventArgs : EventArgs
{
    /// <summary>
    /// Gets the captured frame.
    /// </summary>
    /// <value>
    /// Bitmap containing the frame data. Ownership is transferred to event handler.
    /// </value>
    /// <remarks>
    /// Event handler MUST dispose this Bitmap when done processing.
    /// </remarks>
    public Bitmap Frame { get; }

    /// <summary>
    /// Gets the timestamp when the frame was captured.
    /// </summary>
    /// <value>
    /// UTC timestamp of frame capture.
    /// </value>
    /// <remarks>
    /// Used for latency measurement and temporal ordering of frames.
    /// </remarks>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameReadyEventArgs"/> class.
    /// </summary>
    /// <param name="frame">The captured frame (ownership transferred to handler).</param>
    /// <param name="timestamp">The capture timestamp.</param>
    public FrameReadyEventArgs(Bitmap frame, DateTime timestamp)
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Timestamp = timestamp;
    }
}
