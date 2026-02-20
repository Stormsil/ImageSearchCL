using System.Drawing;
using System.Windows.Forms;

namespace ImageSearchCL.Infrastructure;

internal sealed partial class DebugOverlay
{
    private record DetectionBox(Rectangle Bounds, Color Color, int Thickness, double Confidence, DateTime Timestamp);

    /// <summary>
    /// Transparent overlay window using layered window API.
    /// </summary>
    private partial class OverlayWindow : Form
    {
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOPMOST = 0x8;
        private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

        private const int ULW_ALPHA = 0x02;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        public OverlayWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;

            var bounds = GetTotalScreenBounds();
            Bounds = bounds;

            SetStyle(ControlStyles.Opaque, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Layered window is painted via UpdateLayeredWindow.
        }
    }
}
