using System.Drawing;
using System.Drawing.Imaging;

namespace ImageSearchCL.API;

/// <summary>
/// Represents the reference image (template) to track in video frames.
/// </summary>
/// <remarks>
/// This class encapsulates the template image used for OpenCV template matching.
///
/// Lifecycle Management:
/// - ReferenceImage takes ownership of the provided Bitmap
/// - Caller should NOT dispose the Bitmap after construction
/// - ReferenceImage.Dispose() will dispose the internal Bitmap
/// - Immutable after construction (thread-safe for reading)
///
/// Image Format Requirements:
/// - Supported formats: PixelFormat.Format24bppRgb, Format32bppArgb, Format32bppRgb
/// - Minimum size: 3x3 pixels (OpenCV template matching requirement)
/// - Maximum size: Limited by available memory (typical: &lt;1920x1080)
/// - Color space: RGB or RGBA (alpha channel ignored in matching)
///
/// Performance Considerations:
/// - Larger templates = slower matching (O(W*H) per frame)
/// - Smaller templates = faster but less distinctive
/// - Typical sizes: 20x20 to 200x200 pixels for UI elements
/// - Pre-process images: Crop tightly around distinctive features
/// </remarks>
public sealed class ReferenceImage : IDisposable
{
    private Bitmap? _image;
    private bool _disposed;

    /// <summary>
    /// Gets the reference image bitmap.
    /// </summary>
    /// <value>
    /// Bitmap containing the template image, or null if disposed.
    /// </value>
    /// <remarks>
    /// DO NOT dispose this Bitmap - ReferenceImage owns it.
    /// Thread-safe: Can be read from multiple threads (but do not modify).
    /// </remarks>
    public Bitmap? Image => _image;

    /// <summary>
    /// Gets the width of the reference image in pixels.
    /// </summary>
    /// <value>
    /// Image width, always ≥3 pixels.
    /// </value>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the reference image in pixels.
    /// </summary>
    /// <value>
    /// Image height, always ≥3 pixels.
    /// </value>
    public int Height { get; }

    /// <summary>
    /// Gets the pixel format of the reference image.
    /// </summary>
    /// <value>
    /// One of: Format24bppRgb, Format32bppArgb, Format32bppRgb.
    /// </value>
    public PixelFormat PixelFormat { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceImage"/> class.
    /// </summary>
    /// <param name="image">
    /// The reference image bitmap. Ownership is transferred to ReferenceImage.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="image"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if image dimensions are too small (&lt;3x3) or pixel format is unsupported.
    /// </exception>
    /// <remarks>
    /// After construction:
    /// - ReferenceImage owns the bitmap (caller should NOT dispose it)
    /// - Image is validated for size and format
    /// - Metadata (width, height, format) is cached for fast access
    /// </remarks>
    public ReferenceImage(Bitmap image)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));

        // Validate minimum size (OpenCV template matching requirement)
        if (image.Width < 3 || image.Height < 3)
            throw new ArgumentException(
                $"Reference image must be at least 3x3 pixels (got {image.Width}x{image.Height}).",
                nameof(image));

        // Validate pixel format
        if (!IsSupportedPixelFormat(image.PixelFormat))
            throw new ArgumentException(
                $"Unsupported pixel format: {image.PixelFormat}. " +
                $"Supported formats: Format24bppRgb, Format32bppArgb, Format32bppRgb.",
                nameof(image));

        _image = image;
        Width = image.Width;
        Height = image.Height;
        PixelFormat = image.PixelFormat;
    }

    /// <summary>
    /// Creates a ReferenceImage from a file path.
    /// </summary>
    /// <param name="filePath">Path to the image file (PNG, JPG, BMP, etc.).</param>
    /// <returns>
    /// New ReferenceImage instance loaded from file.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="filePath"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown if file does not exist.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if image is too small or has unsupported format.
    /// </exception>
    /// <remarks>
    /// Convenience factory method for loading from disk.
    /// Supports all image formats supported by System.Drawing.Image.FromFile().
    /// </remarks>
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
            // If ReferenceImage constructor throws, dispose bitmap to prevent leak
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a ReferenceImage from a byte array.
    /// </summary>
    /// <param name="imageData">Raw image data (PNG, JPG, BMP format).</param>
    /// <returns>
    /// New ReferenceImage instance decoded from byte array.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="imageData"/> is null or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if image is too small, has unsupported format, or data is corrupted.
    /// </exception>
    /// <remarks>
    /// Convenience factory method for loading from embedded resources or network.
    /// Supports all image formats supported by System.Drawing.Image.FromStream().
    /// </remarks>
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
            // If ReferenceImage constructor throws, dispose bitmap to prevent leak
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Determines whether the specified pixel format is supported for template matching.
    /// </summary>
    private static bool IsSupportedPixelFormat(PixelFormat format)
    {
        return format == PixelFormat.Format24bppRgb
            || format == PixelFormat.Format32bppArgb
            || format == PixelFormat.Format32bppRgb;
    }

    /// <summary>
    /// Releases all resources used by this ReferenceImage.
    /// </summary>
    /// <remarks>
    /// After disposal:
    /// - Image property returns null
    /// - Width, Height, PixelFormat remain accessible (cached values)
    /// - Safe to call multiple times (idempotent)
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;

        _image?.Dispose();
        _image = null;
        _disposed = true;
    }

    /// <summary>
    /// Returns a string representation of this reference image.
    /// </summary>
    /// <returns>
    /// String in format: "ReferenceImage { 100x50, Format24bppRgb }"
    /// </returns>
    public override string ToString()
    {
        return $"ReferenceImage {{ {Width}x{Height}, {PixelFormat} }}";
    }
}
