using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ImageSearchCL.Infrastructure;

internal sealed partial class DebugOverlay
{
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
            else if (!_window.Visible)
            {
                _window.Show();
                _window.TopMost = true;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DebugOverlay] Error in Show(): {ex.Message}");
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
        catch (Exception ex)
        {
            Trace.WriteLine($"[DebugOverlay] Error in Clear(): {ex.Message}");
        }
    }

    /// <summary>
    /// Registers a detection to be drawn on the overlay.
    /// </summary>
    public void RegisterDetection(API.FindResult result, Color color, int thickness)
    {
        RegisterDetection(result, color, thickness, IntPtr.Zero);
    }

    /// <summary>
    /// Registers a detection to be drawn on the overlay with window coordinate conversion.
    /// </summary>
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

        int screenX = result.X;
        int screenY = result.Y;

        if (windowHandle != IntPtr.Zero)
        {
            RECT dwmRect;
            int hr = DwmGetWindowAttribute(windowHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out dwmRect, Marshal.SizeOf(typeof(RECT)));

            if (hr == 0)
            {
                screenX = dwmRect.Left + result.X;
                screenY = dwmRect.Top + result.Y;
            }
            else if (GetWindowRect(windowHandle, out RECT windowRect))
            {
                screenX = windowRect.Left + result.X;
                screenY = windowRect.Top + result.Y;
            }
        }

        var box = new DetectionBox(
            new Rectangle(screenX, screenY, result.Width, result.Height),
            color,
            thickness,
            result.Confidence,
            DateTime.UtcNow);

        lock (_detectionsLock)
        {
            var now = DateTime.UtcNow;
            _detections.RemoveAll(d => (now - d.Timestamp).TotalMilliseconds > 50);
            _detections.Add(box);
        }

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
        catch (Exception ex)
        {
            Trace.WriteLine($"[DebugOverlay] Error in RegisterDetection(): {ex.Message}");
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
}
