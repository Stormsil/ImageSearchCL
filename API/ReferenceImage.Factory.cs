using System.Drawing;

namespace ImageSearchCL.API;

public sealed partial class ReferenceImage
{
    public static ReferenceImage FromFile(string filePath)
    {
        if (filePath == null)
            throw new ArgumentNullException(nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Reference image file not found: {filePath}", filePath);

        var bitmap = new Bitmap(filePath);
        try
        {
            return new ReferenceImage(bitmap);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public static ReferenceImage FromBytes(byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0)
            throw new ArgumentNullException(nameof(imageData));

        using var stream = new MemoryStream(imageData);
        var bitmap = new Bitmap(stream);
        try
        {
            return new ReferenceImage(bitmap);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
