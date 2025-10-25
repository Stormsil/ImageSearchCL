using System.Drawing;
using ImageSearchCL.API;

namespace ImageSearchCL.WindowCapture;

/// <summary>
/// Internal adapter for WindowCaptureCL.ICaptureSession → ImageSearchCL.API.ICaptureSession
/// Allows using WindowCaptureCL for real screen capture with ImageSearchCL
/// </summary>
internal sealed class WindowCaptureAdapter : ImageSearchCL.API.ICaptureSession
{
    private readonly WindowCaptureCL.ICaptureSession _windowCaptureSession;
    private Bitmap? _lastFrame;
    private readonly object _frameLock = new();

    public event EventHandler<ImageSearchCL.API.FrameReadyEventArgs>? FrameReady;

    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }
    public bool IsCapturing { get; private set; }

    internal WindowCaptureAdapter(WindowCaptureCL.ICaptureSession windowCaptureSession)
    {
        _windowCaptureSession = windowCaptureSession ?? throw new ArgumentNullException(nameof(windowCaptureSession));

        // Get dimensions from SourceInfo
        FrameWidth = _windowCaptureSession.SourceInfo.Width;
        FrameHeight = _windowCaptureSession.SourceInfo.Height;

        // Subscribe to WindowCaptureCL events
        _windowCaptureSession.FrameReady += OnWindowCaptureFrameReady;
        _windowCaptureSession.CaptureError += OnCaptureError;
        _windowCaptureSession.CaptureStopped += OnCaptureStopped;
    }

    private void OnWindowCaptureFrameReady(object? sender, WindowCaptureCL.FrameReadyEventArgs e)
    {
        // Save last frame
        lock (_frameLock)
        {
            _lastFrame?.Dispose();
            _lastFrame = (Bitmap)e.Frame.Clone();
        }

        // Convert WindowCaptureCL event to ImageSearchCL event
        var imageSearchArgs = new ImageSearchCL.API.FrameReadyEventArgs(
            (Bitmap)e.Frame.Clone(),
            e.Timestamp
        );

        FrameReady?.Invoke(this, imageSearchArgs);
    }

    private void OnCaptureError(object? sender, WindowCaptureCL.CaptureErrorEventArgs e)
    {
        // Silently ignore errors
    }

    private void OnCaptureStopped(object? sender, WindowCaptureCL.CaptureStoppedEventArgs e)
    {
        IsCapturing = false;
    }

    public void Start()
    {
        if (IsCapturing)
        {
            throw new InvalidOperationException("Capture already started");
        }

        _windowCaptureSession.StartCapture();
        IsCapturing = true;
    }

    public void Stop()
    {
        if (!IsCapturing)
        {
            throw new InvalidOperationException("Capture not started");
        }

        _windowCaptureSession.StopCapture();
        IsCapturing = false;
    }

    public Bitmap? GetCurrentFrame()
    {
        try
        {
            using var capturedFrame = _windowCaptureSession.CaptureFrame();
            return (Bitmap)capturedFrame.Bitmap.Clone();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (IsCapturing)
        {
            try
            {
                Stop();
            }
            catch
            {
                // Ignore errors during stop
            }
        }

        // Unsubscribe from events
        _windowCaptureSession.FrameReady -= OnWindowCaptureFrameReady;
        _windowCaptureSession.CaptureError -= OnCaptureError;
        _windowCaptureSession.CaptureStopped -= OnCaptureStopped;

        // Clear last frame
        lock (_frameLock)
        {
            _lastFrame?.Dispose();
            _lastFrame = null;
        }

        // Dispose WindowCaptureCL session
        _windowCaptureSession.Dispose();
    }
}
