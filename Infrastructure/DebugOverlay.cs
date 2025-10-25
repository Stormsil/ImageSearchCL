using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ImageSearchCL.Infrastructure;

/// <summary>
/// Transparent, click-through overlay window for visualizing object detection.
/// </summary>
/// <remarks>
/// Uses layered window (WS_EX_LAYERED) with UpdateLayeredWindow for:
/// - Full transparency with alpha channel
/// - Hardware accelerated rendering
/// - Click-through (WS_EX_TRANSPARENT)
/// - Always on top
/// - No flickering
///
/// Threading:
/// - Must be created and accessed from the same thread (STA)
/// - Uses Windows message pump for updates
///
/// Performance:
/// - UpdateLayeredWindow is hardware accelerated
/// - Double-buffered drawing
/// - Minimal CPU impact
/// </remarks>
internal sealed class DebugOverlay : IDisposable
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
        // Create overlay on dedicated STA thread with message pump
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
                if (_instance == null)
                {
                    _instance = new DebugOverlay();
                }
                return _instance;
            }
        }
    }

    /// <summary>
    /// Shows the overlay window (already shown by default).
    /// </summary>
    public void Show()
    {
        if (_disposed || _window == null)
            return;

        try
        {
            if (_window.InvokeRequired)
            {
                _window.BeginInvoke(new Action(() =>
                {
                    if (!_window.Visible)
                    {
                        _window.Show();
                        _window.TopMost = true;
                    }
                }));
            }
            else
            {
                if (!_window.Visible)
                {
                    _window.Show();
                    _window.TopMost = true;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// Hides the overlay window.
    /// </summary>
    public void Hide()
    {
        if (_disposed || _window == null)
            return;

        _window.Invoke(new Action(() =>
        {
            _window.Hide();
        }));
    }

    /// <summary>
    /// Clears all detections from the overlay.
    /// </summary>
    public void Clear()
    {
        if (_disposed || _window == null)
            return;

        lock (_detectionsLock)
        {
            _detections.Clear();
        }

        // Update overlay to remove all drawings
        try
        {
            if (_window.InvokeRequired)
            {
                _window.BeginInvoke(new Action(() => _window.Invalidate()));
            }
            else
            {
                _window.Invalidate();
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Registers a detection to be drawn on the overlay.
    /// </summary>
    /// <param name="result">Detection result with position and size.</param>
    /// <param name="color">Color for the bounding box.</param>
    /// <param name="thickness">Line thickness in pixels.</param>
    public void RegisterDetection(API.FindResult result, Color color, int thickness)
    {
        RegisterDetection(result, color, thickness, IntPtr.Zero);
    }

    /// <summary>
    /// Registers a detection to be drawn on the overlay with window coordinate conversion.
    /// </summary>
    /// <param name="result">Detection result with position and size (in window coordinates if windowHandle is provided).</param>
    /// <param name="color">Color for the bounding box.</param>
    /// <param name="thickness">Line thickness in pixels.</param>
    /// <param name="windowHandle">Window handle for coordinate conversion (IntPtr.Zero for screen coordinates).</param>
    public void RegisterDetection(API.FindResult result, Color color, int thickness, IntPtr windowHandle)
    {
        if (_disposed || _window == null)
        {
            return;
        }

        if (!_window.IsHandleCreated)
        {
            return;
        }

        // Convert window coordinates to screen coordinates if window handle is provided
        int screenX = result.X;
        int screenY = result.Y;

        if (windowHandle != IntPtr.Zero)
        {
            // Windows 10/11 have invisible borders for shadows that GetWindowRect includes
            // Use DwmGetWindowAttribute to get actual visible window bounds
            RECT dwmRect;
            int hr = DwmGetWindowAttribute(windowHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out dwmRect, Marshal.SizeOf(typeof(RECT)));

            if (hr == 0) // S_OK
            {
                // DWM rect gives us the actual visible window position without shadow borders
                // WindowCaptureCL captures this visible window
                screenX = dwmRect.Left + result.X;
                screenY = dwmRect.Top + result.Y;
            }
            else
            {
                // Fallback to GetWindowRect if DWM API fails
                if (GetWindowRect(windowHandle, out RECT windowRect))
                {
                    screenX = windowRect.Left + result.X;
                    screenY = windowRect.Top + result.Y;
                }
            }
        }

        var box = new DetectionBox(
            new Rectangle(screenX, screenY, result.Width, result.Height),
            color,
            thickness,
            result.Confidence,
            DateTime.UtcNow
        );


        lock (_detectionsLock)
        {
            // Remove old detections (older than 50ms)
            var now = DateTime.UtcNow;
            _detections.RemoveAll(d => (now - d.Timestamp).TotalMilliseconds > 50);

            // Add new detection
            _detections.Add(box);
        }

        // Update overlay on UI thread
        try
        {
            if (_window.InvokeRequired)
            {
                _window.BeginInvoke(new Action(() =>
                {
                    _window.Invalidate();
                }));
            }
            else
            {
                _window.Invalidate();
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_window != null)
        {
            _window.Invoke(new Action(() =>
            {
                _window.Close();
                _window.Dispose();
            }));
        }

        _windowCreated.Dispose();

        lock (_instanceLock)
        {
            if (_instance == this)
                _instance = null;
        }
    }

    private record DetectionBox(Rectangle Bounds, Color Color, int Thickness, double Confidence, DateTime Timestamp);

    /// <summary>
    /// Transparent overlay window using layered window API.
    /// </summary>
    private class OverlayWindow : Form
    {
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOPMOST = 0x8;
        private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd,
            IntPtr hdcDst,
            ref Point pptDst,
            ref Size psize,
            IntPtr hdcSrc,
            ref Point pptSrc,
            uint crKey,
            ref BLENDFUNCTION pblend,
            uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        private const int ULW_ALPHA = 0x02;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        public OverlayWindow()
        {
            // Window setup
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;

            // Fullscreen bounds
            var bounds = GetTotalScreenBounds();
            Bounds = bounds;

            // Set layered window style
            SetStyle(ControlStyles.Opaque, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);


            // Initial transparent render
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Don't use default painting - we use UpdateLayeredWindow
        }

        public new void Invalidate()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }


            // Get detections to draw through singleton
            DetectionBox[] detections;
            lock (DebugOverlay._instanceLock)
            {
                if (DebugOverlay._instance != null)
                {
                    lock (DebugOverlay._instance._detectionsLock)
                    {
                        detections = DebugOverlay._instance._detections.ToArray();
                    }
                }
                else
                {
                    detections = Array.Empty<DetectionBox>();
                }
            }

            // Create bitmap for layered window
            using (var bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            {

                // Clear to fully transparent
                graphics.Clear(Color.Transparent);

                // Enable antialiasing
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw detection boxes as 4 separate edges with offset
                foreach (var detection in detections)
                {

                    // Offset от границ детекции (чтобы не перекрывать объект)
                    int offset = 5;
                    int edgeLength = 15; // Длина каждой грани
                    float lineThickness = 1.5f; // Тонкие линии

                    var bounds = detection.Bounds;
                    using (var pen = new Pen(detection.Color, lineThickness))
                    {
                        // Верхняя левая угловая линия (горизонтальная)
                        graphics.DrawLine(pen,
                            bounds.Left - offset, bounds.Top - offset,
                            bounds.Left - offset + edgeLength, bounds.Top - offset);

                        // Верхняя левая угловая линия (вертикальная)
                        graphics.DrawLine(pen,
                            bounds.Left - offset, bounds.Top - offset,
                            bounds.Left - offset, bounds.Top - offset + edgeLength);

                        // Верхняя правая угловая линия (горизонтальная)
                        graphics.DrawLine(pen,
                            bounds.Right + offset - edgeLength, bounds.Top - offset,
                            bounds.Right + offset, bounds.Top - offset);

                        // Верхняя правая угловая линия (вертикальная)
                        graphics.DrawLine(pen,
                            bounds.Right + offset, bounds.Top - offset,
                            bounds.Right + offset, bounds.Top - offset + edgeLength);

                        // Нижняя левая угловая линия (вертикальная)
                        graphics.DrawLine(pen,
                            bounds.Left - offset, bounds.Bottom + offset - edgeLength,
                            bounds.Left - offset, bounds.Bottom + offset);

                        // Нижняя левая угловая линия (горизонтальная)
                        graphics.DrawLine(pen,
                            bounds.Left - offset, bounds.Bottom + offset,
                            bounds.Left - offset + edgeLength, bounds.Bottom + offset);

                        // Нижняя правая угловая линия (вертикальная)
                        graphics.DrawLine(pen,
                            bounds.Right + offset, bounds.Bottom + offset - edgeLength,
                            bounds.Right + offset, bounds.Bottom + offset);

                        // Нижняя правая угловая линия (горизонтальная)
                        graphics.DrawLine(pen,
                            bounds.Right + offset - edgeLength, bounds.Bottom + offset,
                            bounds.Right + offset, bounds.Bottom + offset);
                    }

                    // Рисуем текст с процентом совпадения
                    string confidenceText = $"{detection.Confidence * 100:F0}%";

                    // Компактный шрифт
                    float fontSize = 8;
                    using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(detection.Color))
                    {
                        // Измеряем размер текста
                        var textSize = graphics.MeasureString(confidenceText, font);

                        // Позиция текста: к верхнему правому углу с внешней стороны
                        float textX = bounds.Right + offset + 3;
                        float textY = bounds.Top - offset - textSize.Height + 2;

                        // Проверяем, не выходит ли текст за границы экрана
                        if (textX + textSize.Width > Width)
                        {
                            // Если не помещается справа, рисуем слева
                            textX = bounds.Left - offset - textSize.Width - 3;
                        }

                        // Рисуем полупрозрачный фон для текста
                        var bgRect = new RectangleF(textX - 2, textY - 2, textSize.Width + 4, textSize.Height + 4);
                        using (var bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                        {
                            graphics.FillRectangle(bgBrush, bgRect);
                        }

                        // Рисуем текст
                        graphics.DrawString(confidenceText, font, textBrush, textX, textY);
                    }
                }

                // Update layered window
                UpdateLayeredWindowFromBitmap(bitmap);
            }
        }

        private void UpdateLayeredWindowFromBitmap(Bitmap bitmap)
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }


            IntPtr screenDc = IntPtr.Zero;
            IntPtr memDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBitmap = IntPtr.Zero;

            try
            {
                // Get screen DC
                screenDc = GetDC(IntPtr.Zero);
                memDc = CreateCompatibleDC(screenDc);


                // Create GDI bitmap
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                hOldBitmap = SelectObject(memDc, hBitmap);


                // Setup blend function for per-pixel alpha
                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                // Update layered window
                var size = new Size(bitmap.Width, bitmap.Height);
                var pointSource = new Point(0, 0);
                var pointDest = new Point(Left, Top);


                bool result = UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    ref pointDest,
                    ref size,
                    memDc,
                    ref pointSource,
                    0,
                    ref blend,
                    ULW_ALPHA);

            }
            finally
            {
                // Cleanup
                if (hOldBitmap != IntPtr.Zero)
                    SelectObject(memDc, hOldBitmap);

                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);

                if (memDc != IntPtr.Zero)
                    DeleteDC(memDc);

                if (screenDc != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private static Rectangle GetTotalScreenBounds()
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (var screen in Screen.AllScreens)
            {
                minX = Math.Min(minX, screen.Bounds.Left);
                minY = Math.Min(minY, screen.Bounds.Top);
                maxX = Math.Max(maxX, screen.Bounds.Right);
                maxY = Math.Max(maxY, screen.Bounds.Bottom);
            }

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        // GDI32 imports
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    }

    // P/Invoke for window coordinate conversion
    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
