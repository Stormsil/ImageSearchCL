using System.Drawing.Imaging;

namespace ImageSearchCL.API;

public sealed partial class ReferenceImage
{
    private static bool IsSupportedPixelFormat(PixelFormat format)
    {
        return format == PixelFormat.Format24bppRgb
            || format == PixelFormat.Format32bppArgb
            || format == PixelFormat.Format32bppRgb;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _image?.Dispose();
        _image = null;
        _disposed = true;
    }

    public override string ToString()
    {
        return $"ReferenceImage {{ {Width}x{Height}, {PixelFormat} }}";
    }
}
