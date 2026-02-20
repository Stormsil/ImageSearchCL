using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ImageSearchCL.Infrastructure;

internal sealed partial class DebugOverlay
{
    private partial class OverlayWindow
    {
        public new void Invalidate()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

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

            using (var bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (var detection in detections)
                {
                    const int offset = 5;
                    const int edgeLength = 15;
                    const float lineThickness = 1.5f;

                    var bounds = detection.Bounds;
                    using (var pen = new Pen(detection.Color, lineThickness))
                    {
                        graphics.DrawLine(pen, bounds.Left - offset, bounds.Top - offset, bounds.Left - offset + edgeLength, bounds.Top - offset);
                        graphics.DrawLine(pen, bounds.Left - offset, bounds.Top - offset, bounds.Left - offset, bounds.Top - offset + edgeLength);
                        graphics.DrawLine(pen, bounds.Right + offset - edgeLength, bounds.Top - offset, bounds.Right + offset, bounds.Top - offset);
                        graphics.DrawLine(pen, bounds.Right + offset, bounds.Top - offset, bounds.Right + offset, bounds.Top - offset + edgeLength);
                        graphics.DrawLine(pen, bounds.Left - offset, bounds.Bottom + offset - edgeLength, bounds.Left - offset, bounds.Bottom + offset);
                        graphics.DrawLine(pen, bounds.Left - offset, bounds.Bottom + offset, bounds.Left - offset + edgeLength, bounds.Bottom + offset);
                        graphics.DrawLine(pen, bounds.Right + offset, bounds.Bottom + offset - edgeLength, bounds.Right + offset, bounds.Bottom + offset);
                        graphics.DrawLine(pen, bounds.Right + offset - edgeLength, bounds.Bottom + offset, bounds.Right + offset, bounds.Bottom + offset);
                    }

                    var confidenceText = $"{detection.Confidence * 100:F0}%";
                    const float fontSize = 8;

                    using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(detection.Color))
                    {
                        var textSize = graphics.MeasureString(confidenceText, font);
                        float textX = bounds.Right + offset + 3;
                        float textY = bounds.Top - offset - textSize.Height + 2;

                        if (textX + textSize.Width > Width)
                        {
                            textX = bounds.Left - offset - textSize.Width - 3;
                        }

                        var bgRect = new RectangleF(textX - 2, textY - 2, textSize.Width + 4, textSize.Height + 4);
                        using (var bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                        {
                            graphics.FillRectangle(bgBrush, bgRect);
                        }

                        graphics.DrawString(confidenceText, font, textBrush, textX, textY);
                    }
                }

                UpdateLayeredWindowFromBitmap(bitmap);
            }
        }

        private void UpdateLayeredWindowFromBitmap(Bitmap bitmap)
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

            var screenDc = IntPtr.Zero;
            var memDc = IntPtr.Zero;
            var hBitmap = IntPtr.Zero;
            var hOldBitmap = IntPtr.Zero;

            try
            {
                screenDc = GetDC(IntPtr.Zero);
                memDc = CreateCompatibleDC(screenDc);

                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                hOldBitmap = SelectObject(memDc, hBitmap);

                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA,
                };

                var size = new Size(bitmap.Width, bitmap.Height);
                var pointSource = new Point(0, 0);
                var pointDest = new Point(Left, Top);

                _ = UpdateLayeredWindow(
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
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            foreach (var screen in Screen.AllScreens)
            {
                minX = Math.Min(minX, screen.Bounds.Left);
                minY = Math.Min(minY, screen.Bounds.Top);
                maxX = Math.Max(maxX, screen.Bounds.Right);
                maxY = Math.Max(maxY, screen.Bounds.Bottom);
            }

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
