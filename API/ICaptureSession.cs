using System.Drawing;

namespace ImageSearchCL.API;

/// <summary>
/// Represents a video frame source that provides frames to tracking sessions.
/// </summary>
/// <remarks>
/// This interface abstracts away the concrete frame capture implementation,
/// enabling ImageSearchCL to work with any capture library (screen capture,
/// camera feeds, video files, test fixtures, etc.).
///
/// Implementors MUST:
/// - Emit <see cref="FrameReady"/> event when new frames are available
/// - Provide frames in standard Bitmap format (RGB or RGBA)
/// - Handle frame lifecycle (caller takes ownership of Bitmap)
/// - Support graceful disposal (stop capture, release resources)
///
/// Implementors SHOULD:
/// - Emit frames at consistent intervals (e.g., 30 FPS, 60 FPS)
/// - Avoid blocking the FrameReady event handlers
/// - Provide frame dimensions that match capture source resolution
/// </remarks>
public interface ICaptureSession : IDisposable
{
    /// <summary>
    /// Raised when a new frame is available for processing.
    /// </summary>
    /// <remarks>
    /// Event Semantics:
    /// - Event args contain a Bitmap representing the captured frame
    /// - Bitmap ownership is transferred to the event handler (caller must dispose)
    /// - Event should be raised on a background thread (not UI thread)
    /// - Event handlers should return quickly (frame processing happens elsewhere)
    ///
    /// Performance Expectations:
    /// - For real-time tracking: Emit at least 30 FPS (every ~33ms)
    /// - For high-performance scenarios: Up to 60 FPS (every ~16ms)
    /// - For low-latency scenarios: Minimize delay between capture and event emission
    ///
    /// Frame Format:
    /// - Bitmap.PixelFormat: PixelFormat.Format24bppRgb or Format32bppArgb
    /// - Bitmap dimensions should match capture source (e.g., 1920x1080 for FHD screen)
    /// - Bitmap should represent the current state of the capture source
    ///
    /// Thread Safety:
    /// - May be raised from any thread
    /// - Tracking session will handle thread marshalling for its own events
    /// </remarks>
    event EventHandler<FrameReadyEventArgs> FrameReady;

    /// <summary>
    /// Gets the width of captured frames in pixels.
    /// </summary>
    /// <value>
    /// Frame width, typically matching capture source resolution (e.g., 1920 for FHD).
    /// </value>
    /// <remarks>
    /// This value should remain constant for the lifetime of the capture session.
    /// If capture source resolution changes, create a new ICaptureSession instance.
    /// </remarks>
    int FrameWidth { get; }

    /// <summary>
    /// Gets the height of captured frames in pixels.
    /// </summary>
    /// <value>
    /// Frame height, typically matching capture source resolution (e.g., 1080 for FHD).
    /// </value>
    /// <remarks>
    /// This value should remain constant for the lifetime of the capture session.
    /// If capture source resolution changes, create a new ICaptureSession instance.
    /// </remarks>
    int FrameHeight { get; }

    /// <summary>
    /// Gets a value indicating whether the capture session is currently active.
    /// </summary>
    /// <value>
    /// True if capturing frames and emitting FrameReady events, otherwise false.
    /// </value>
    /// <remarks>
    /// Implementors should set this to true when capture starts and false when
    /// capture stops (paused, stopped, or disposed).
    /// </remarks>
    bool IsCapturing { get; }

    /// <summary>
    /// Starts capturing frames and emitting <see cref="FrameReady"/> events.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if session is already capturing or if capture cannot be started.
    /// </exception>
    /// <remarks>
    /// After calling Start():
    /// - IsCapturing should be true
    /// - FrameReady events begin emitting
    /// - Frames should be captured at consistent intervals
    ///
    /// This method should return quickly (non-blocking).
    /// Actual frame capture should happen on a background thread.
    /// </remarks>
    void Start();

    /// <summary>
    /// Stops capturing frames and stops emitting <see cref="FrameReady"/> events.
    /// </summary>
    /// <remarks>
    /// After calling Stop():
    /// - IsCapturing should be false
    /// - No more FrameReady events are emitted
    /// - Resources may be released (depending on implementation)
    ///
    /// This method should return quickly (non-blocking).
    /// Session can be restarted with Start() unless disposed.
    /// </remarks>
    void Stop();

    /// <summary>
    /// Gets the current frame synchronously without waiting for FrameReady event.
    /// </summary>
    /// <returns>
    /// Bitmap representing the current frame, or null if no frame is available.
    /// Caller takes ownership of the Bitmap and must dispose it.
    /// </returns>
    /// <remarks>
    /// This method is optional for implementors (can throw NotSupportedException).
    /// Useful for:
    /// - Initial frame grab before event subscription
    /// - On-demand polling scenarios
    /// - Testing and debugging
    ///
    /// Thread Safety: Should be safe to call from any thread.
    /// Performance: May block briefly to capture frame (typically &lt;10ms).
    /// </remarks>
    Bitmap? GetCurrentFrame();
}
