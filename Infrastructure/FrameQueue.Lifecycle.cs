namespace ImageSearchCL.Infrastructure;

internal sealed partial class FrameQueue
{
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        var frame = Interlocked.Exchange(ref _currentFrame, null);
        frame?.Dispose();
    }
}
