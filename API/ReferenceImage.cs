using System.Drawing;
using System.Drawing.Imaging;

namespace ImageSearchCL.API;

/// <summary>
/// Represents the reference image (template) to track in video frames.
/// </summary>
public sealed partial class ReferenceImage : IDisposable
{
    private Bitmap? _image;
    private bool _disposed;

    public Bitmap? Image => _image;

    public int Width { get; }

    public int Height { get; }

    public PixelFormat PixelFormat { get; }

    public ReferenceImage(Bitmap image)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));

        if (image.Width < 3 || image.Height < 3)
            throw new ArgumentException(
                $"Reference image must be at least 3x3 pixels (got {image.Width}x{image.Height}).",
                nameof(image));

        if (!IsSupportedPixelFormat(image.PixelFormat))
            throw new ArgumentException(
                $"Unsupported pixel format: {image.PixelFormat}. " +
                "Supported formats: Format24bppRgb, Format32bppArgb, Format32bppRgb.",
                nameof(image));

        _image = image;
        Width = image.Width;
        Height = image.Height;
        PixelFormat = image.PixelFormat;
    }
}
