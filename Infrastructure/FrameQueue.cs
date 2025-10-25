using System.Drawing;

namespace ImageSearchCL.Infrastructure;

/// <summary>
/// Thread-safe single-slot frame buffer using atomic exchange pattern.
/// </summary>
/// <remarks>
/// Architecture Role:
/// - Infrastructure layer (low-level primitive for inter-thread communication)
/// - Implements single-slot buffering with automatic frame disposal
/// - Used by TrackingSession to decouple frame capture from processing
///
/// Threading Model:
/// - Producer thread (ICaptureSession.FrameReady): Calls Enqueue()
/// - Consumer thread (TrackingLoop): Calls Dequeue()
/// - Lock-free using Interlocked.Exchange for atomic slot replacement
/// - Only one frame stored at a time (latest frame wins)
///
/// Memory Management:
/// - Automatically disposes replaced frames (prevents memory leaks)
/// - Caller owns dequeued frames (must dispose after use)
/// - Clear() disposes any buffered frame
///
/// Performance:
/// - Zero allocations per frame (except bitmap itself)
/// - No blocking, no contention (lock-free)
/// - Constant O(1) time for enqueue/dequeue
/// - Skips frames naturally when processing falls behind capture rate
///
/// Example:
/// <code>
/// var queue = new FrameQueue();
///
/// // Producer thread
/// captureSession.FrameReady += (s, e) => queue.Enqueue(e.Frame);
///
/// // Consumer thread
/// while (running)
/// {
///     var frame = queue.Dequeue();
///     if (frame != null)
///     {
///         ProcessFrame(frame);
///         frame.Dispose();
///     }
/// }
/// </code>
/// </remarks>
internal sealed class FrameQueue : IDisposable
{
    private Bitmap? _currentFrame;
    private bool _disposed;

    /// <summary>
    /// Gets the number of frames currently buffered (0 or 1).
    /// </summary>
    /// <remarks>
    /// Thread-safe: Can be read from any thread.
    /// Useful for diagnostics and testing.
    /// </remarks>
    public int Count => _currentFrame != null ? 1 : 0;

    /// <summary>
    /// Enqueues a new frame, replacing and disposing any existing frame.
    /// </summary>
    /// <param name="frame">The frame to enqueue. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="frame"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this FrameQueue has been disposed.
    /// </exception>
    /// <remarks>
    /// Thread-safe: Can be called from any thread (typically ICaptureSession.FrameReady).
    ///
    /// Behavior:
    /// - Atomically replaces current frame with new frame
    /// - Disposes old frame if present (prevents memory leaks)
    /// - New frame becomes available for Dequeue()
    /// - If processing is slower than capture rate, intermediate frames are skipped
    ///
    /// Performance:
    /// - Lock-free using Interlocked.Exchange
    /// - O(1) time complexity
    /// - No blocking or contention
    /// </remarks>
    public void Enqueue(Bitmap frame)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));
        if (_disposed)
            throw new ObjectDisposedException(nameof(FrameQueue));

        // Atomically replace current frame with new frame
        var oldFrame = Interlocked.Exchange(ref _currentFrame, frame);

        // Dispose replaced frame (if any)
        oldFrame?.Dispose();
    }

    /// <summary>
    /// Dequeues and removes the current frame, or returns null if queue is empty.
    /// </summary>
    /// <returns>
    /// The current frame, or null if no frame is available.
    /// Caller owns the returned bitmap and must dispose it.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this FrameQueue has been disposed.
    /// </exception>
    /// <remarks>
    /// Thread-safe: Can be called from any thread (typically TrackingLoop).
    ///
    /// Behavior:
    /// - Atomically removes and returns current frame
    /// - Returns null if queue is empty
    /// - Caller MUST dispose returned frame to prevent memory leaks
    /// - Non-blocking (returns immediately even if queue is empty)
    ///
    /// Performance:
    /// - Lock-free using Interlocked.Exchange
    /// - O(1) time complexity
    /// - No blocking or contention
    ///
    /// Example:
    /// <code>
    /// var frame = queue.Dequeue();
    /// if (frame != null)
    /// {
    ///     try
    ///     {
    ///         ProcessFrame(frame);
    ///     }
    ///     finally
    ///     {
    ///         frame.Dispose(); // Always dispose!
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public Bitmap? Dequeue()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FrameQueue));

        // Atomically remove current frame and replace with null
        return Interlocked.Exchange(ref _currentFrame, null);
    }

    /// <summary>
    /// Peeks at the current frame without removing it.
    /// </summary>
    /// <returns>
    /// The current frame, or null if queue is empty.
    /// DO NOT dispose - frame is still owned by the queue.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this FrameQueue has been disposed.
    /// </exception>
    /// <remarks>
    /// Thread-safe: Can be called from any thread.
    ///
    /// WARNING:
    /// - Returned frame is still owned by the queue
    /// - Do NOT dispose the returned frame
    /// - Frame may be replaced by Enqueue() at any time
    /// - Use for read-only inspection only
    ///
    /// Prefer Dequeue() for consuming frames.
    /// </remarks>
    public Bitmap? Peek()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FrameQueue));

        return _currentFrame; // Volatile read (reference types are naturally atomic)
    }

    /// <summary>
    /// Clears the queue by removing and disposing any buffered frame.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this FrameQueue has been disposed.
    /// </exception>
    /// <remarks>
    /// Thread-safe: Can be called from any thread.
    ///
    /// Behavior:
    /// - Atomically removes current frame
    /// - Disposes removed frame (if any)
    /// - Queue becomes empty after this call
    /// - Idempotent (safe to call on empty queue)
    /// </remarks>
    public void Clear()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FrameQueue));

        // Atomically remove and dispose current frame
        var frame = Interlocked.Exchange(ref _currentFrame, null);
        frame?.Dispose();
    }

    /// <summary>
    /// Releases all resources used by this FrameQueue.
    /// </summary>
    /// <remarks>
    /// After disposal:
    /// - Disposes any buffered frame
    /// - All methods throw ObjectDisposedException
    /// - Safe to call multiple times (idempotent)
    ///
    /// Thread-safe: Can be called from any thread.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Dispose any buffered frame
        var frame = Interlocked.Exchange(ref _currentFrame, null);
        frame?.Dispose();
    }
}
