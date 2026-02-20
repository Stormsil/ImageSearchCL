using System.Drawing;

namespace ImageSearchCL.Infrastructure;

internal sealed partial class FrameQueue : IDisposable
{
    private Bitmap? _currentFrame;
    private bool _disposed;

    public int Count => _currentFrame != null ? 1 : 0;
}
