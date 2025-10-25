using System.Drawing;
using ImageSearchCL.API;
using WindowCaptureCL;

namespace ImageSearchCL.Demo;

/// <summary>
/// Адаптер для WindowCaptureCL.ICaptureSession → ImageSearchCL.API.ICaptureSession
/// Позволяет использовать WindowCaptureCL для реального захвата экрана с ImageSearchCL
/// </summary>
public class ScreenCaptureAdapter : ImageSearchCL.API.ICaptureSession
{
    private readonly WindowCaptureCL.ICaptureSession _windowCaptureSession;
    private Bitmap? _lastFrame;
    private readonly object _frameLock = new();

    public event EventHandler<ImageSearchCL.API.FrameReadyEventArgs>? FrameReady;

    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }
    public bool IsCapturing { get; private set; }

    /// <summary>
    /// Создает адаптер для захвата монитора
    /// </summary>
    /// <param name="monitorIndex">Индекс монитора (0 = основной)</param>
    /// <param name="targetFps">Целевой FPS (по умолчанию 30)</param>
    public static ScreenCaptureAdapter FromScreen(int monitorIndex = 0, int targetFps = 30)
    {
        var config = new CaptureConfiguration
        {
            MaxFramesPerSecond = targetFps
        };

        var session = Capture.FromScreen(monitorIndex);
        session.UpdateConfiguration(config);

        return new ScreenCaptureAdapter(session);
    }

    /// <summary>
    /// Создает адаптер для захвата окна
    /// </summary>
    /// <param name="windowHandle">Handle окна</param>
    /// <param name="targetFps">Целевой FPS (по умолчанию 30)</param>
    public static ScreenCaptureAdapter FromWindow(IntPtr windowHandle, int targetFps = 30)
    {
        var config = new CaptureConfiguration
        {
            MaxFramesPerSecond = targetFps
        };

        var session = Capture.FromWindow(windowHandle);
        session.UpdateConfiguration(config);

        return new ScreenCaptureAdapter(session);
    }

    /// <summary>
    /// Создает адаптер для захвата региона экрана
    /// </summary>
    /// <param name="monitorIndex">Индекс монитора</param>
    /// <param name="region">Регион для захвата</param>
    /// <param name="targetFps">Целевой FPS (по умолчанию 30)</param>
    public static ScreenCaptureAdapter FromScreenRegion(int monitorIndex, Rectangle region, int targetFps = 30)
    {
        var config = new CaptureConfiguration
        {
            MaxFramesPerSecond = targetFps
        };

        var session = Capture.FromScreenRegion(monitorIndex, region);
        session.UpdateConfiguration(config);

        return new ScreenCaptureAdapter(session);
    }

    private ScreenCaptureAdapter(WindowCaptureCL.ICaptureSession windowCaptureSession)
    {
        _windowCaptureSession = windowCaptureSession ?? throw new ArgumentNullException(nameof(windowCaptureSession));

        // Получаем размеры из SourceInfo
        FrameWidth = _windowCaptureSession.SourceInfo.Width;
        FrameHeight = _windowCaptureSession.SourceInfo.Height;

        // Подписываемся на события WindowCaptureCL
        _windowCaptureSession.FrameReady += OnWindowCaptureFrameReady;
        _windowCaptureSession.CaptureError += OnCaptureError;
        _windowCaptureSession.CaptureStopped += OnCaptureStopped;
    }

    private void OnWindowCaptureFrameReady(object? sender, WindowCaptureCL.FrameReadyEventArgs e)
    {
        // Сохраняем последний кадр
        lock (_frameLock)
        {
            _lastFrame?.Dispose();
            _lastFrame = (Bitmap)e.Frame.Clone();
        }

        // Преобразуем событие WindowCaptureCL в событие ImageSearchCL
        var imageSearchArgs = new ImageSearchCL.API.FrameReadyEventArgs(
            (Bitmap)e.Frame.Clone(),
            e.Timestamp
        );

        FrameReady?.Invoke(this, imageSearchArgs);
    }

    private void OnCaptureError(object? sender, WindowCaptureCL.CaptureErrorEventArgs e)
    {
        Console.WriteLine($"⚠️  Ошибка захвата: {e.Exception.Message}");
    }

    private void OnCaptureStopped(object? sender, WindowCaptureCL.CaptureStoppedEventArgs e)
    {
        IsCapturing = false;
        Console.WriteLine($"⏸️  Захват остановлен: {e.Reason}");
    }

    public void Start()
    {
        if (IsCapturing)
        {
            throw new InvalidOperationException("Захват уже запущен");
        }

        _windowCaptureSession.StartCapture();
        IsCapturing = true;
    }

    public void Stop()
    {
        if (!IsCapturing)
        {
            throw new InvalidOperationException("Захват не запущен");
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
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Ошибка захвата кадра: {ex.Message}");
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
                // Игнорируем ошибки при остановке
            }
        }

        // Отписываемся от событий
        _windowCaptureSession.FrameReady -= OnWindowCaptureFrameReady;
        _windowCaptureSession.CaptureError -= OnCaptureError;
        _windowCaptureSession.CaptureStopped -= OnCaptureStopped;

        // Очищаем последний кадр
        lock (_frameLock)
        {
            _lastFrame?.Dispose();
            _lastFrame = null;
        }

        // Освобождаем WindowCaptureCL сессию
        _windowCaptureSession.Dispose();
    }
}
