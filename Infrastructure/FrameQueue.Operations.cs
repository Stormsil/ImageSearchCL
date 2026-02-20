using System.Drawing;

namespace ImageSearchCL.Infrastructure;

internal sealed partial class FrameQueue
{
    public void Enqueue(Bitmap frame)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));
        if (_disposed)
            throw new ObjectDisposedException(nameof(FrameQueue));

        var oldFrame = Interlocked.Exchange(ref _currentFrame, frame);
        oldFrame?.Dispose();
    }

    public Bitmap? Dequeue()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FrameQueue));

        return Interlocked.Exchange(ref _currentFrame, null);
    }

    public Bitmap? Peek()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FrameQueue));

        return _currentFrame;
    }

    public void Clear()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FrameQueue));

        var frame = Interlocked.Exchange(ref _currentFrame, null);
        frame?.Dispose();
    }
}
