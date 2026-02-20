namespace ImageSearchCL.Infrastructure;

/// <summary>
/// Transparent, click-through overlay window for visualizing object detection.
/// </summary>
internal sealed partial class DebugOverlay : IDisposable
{
    private static DebugOverlay? _instance;
    private static readonly object _instanceLock = new object();

    private OverlayWindow? _window;
    private Thread? _uiThread;
    private readonly List<DetectionBox> _detections = new();
    private readonly object _detectionsLock = new();
    private readonly ManualResetEventSlim _windowCreated = new(false);
    private bool _disposed;

    private DebugOverlay()
    {
        _uiThread = new Thread(() =>
        {
            _window = new OverlayWindow();
            _window.TopMost = true;
            _window.Show();
            _windowCreated.Set();
            Application.Run(_window);
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.IsBackground = true;
        _uiThread.Start();

        _windowCreated.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Gets the singleton instance of the debug overlay.
    /// </summary>
    public static DebugOverlay Instance
    {
        get
        {
            lock (_instanceLock)
            {
                _instance ??= new DebugOverlay();
                return _instance;
            }
        }
    }
}
