using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ImageSearchCL.Infrastructure;

internal static partial class TemplateMatchingEngine
{
    internal sealed class MatchResult
    {
        public int X { get; }

        public int Y { get; }

        public int Width { get; }

        public int Height { get; }

        public double Confidence { get; }

        public MatchResult(int x, int y, int width, int height, double confidence)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Confidence = confidence;
        }
    }

    private static void ValidateSearchInput(Bitmap frame, Bitmap template)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (template.Width > frame.Width || template.Height > frame.Height)
        {
            throw new ArgumentException(
                $"Template ({template.Width}x{template.Height}) cannot be larger than frame ({frame.Width}x{frame.Height}).");
        }
    }

    private static Mat ConvertToMat(Bitmap bitmap)
    {
        if (bitmap.PixelFormat != PixelFormat.Format24bppRgb &&
            bitmap.PixelFormat != PixelFormat.Format32bppArgb &&
            bitmap.PixelFormat != PixelFormat.Format32bppRgb)
        {
            var converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(converted))
            {
                g.DrawImage(bitmap, 0, 0);
            }

            return BitmapConverter.ToMat(converted);
        }

        return BitmapConverter.ToMat(bitmap);
    }
}
