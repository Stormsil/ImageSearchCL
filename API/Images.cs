using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImageSearchCL.API;

/// <summary>
/// Manages a collection of reference images accessible by name.
/// </summary>
/// <remarks>
/// This class provides centralized management of reference images, allowing you to:
/// - Load multiple images from a directory
/// - Access images by name instead of file paths
/// - Manage a reusable image collection for multiple tracking sessions
///
/// Thread Safety:
/// - This class is NOT thread-safe by default
/// - If accessing from multiple threads, external synchronization is required
/// - For read-only access after initialization, no synchronization needed
///
/// Naming Convention:
/// - Image names are case-insensitive (normalized to lowercase)
/// - Names are derived from filenames without extension
/// - Example: "button.png" → name "button"
///
/// Usage:
/// <code>
/// // Load all images from a directory
/// using var images = Images.FromDirectory(@"C:\MyApp\Templates");
///
/// // Access by name
/// var buttonImage = images["button"];
/// var session = Search.For(buttonImage).In(capture);
///
/// // Or add manually
/// var customImages = new Images();
/// customImages.Add("logo", ReferenceImage.FromFile("logo.png"));
/// </code>
/// </remarks>
public sealed class Images : IDisposable
{
    private readonly Dictionary<string, ReferenceImage> _images;
    private bool _disposed;

    /// <summary>
    /// Initializes a new empty instance of the <see cref="Images"/> class.
    /// </summary>
    public Images()
    {
        // Use case-insensitive comparer for image names
        _images = new Dictionary<string, ReferenceImage>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the reference image with the specified name.
    /// </summary>
    /// <param name="name">The name of the image (case-insensitive).</param>
    /// <returns>The reference image associated with the specified name.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no image with the specified name exists in the collection.
    /// The exception message includes a list of available image names.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the collection has been disposed.
    /// </exception>
    /// <example>
    /// <code>
    /// using var images = Images.FromDirectory("templates");
    /// var buttonImage = images["button"]; // Case-insensitive
    /// var logoImage = images["LOGO"];     // Also works
    /// </code>
    /// </example>
    public ReferenceImage this[string name]
    {
        get
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (_disposed)
                throw new ObjectDisposedException(nameof(Images));

            if (!_images.TryGetValue(name, out var image))
            {
                var availableNames = string.Join(", ", _images.Keys.OrderBy(k => k));
                throw new KeyNotFoundException(
                    $"Image '{name}' not found in collection. Available images: {(availableNames.Length > 0 ? availableNames : "(none)")}");
            }

            return image;
        }
    }

    /// <summary>
    /// Gets the names of all images in the collection.
    /// </summary>
    /// <value>
    /// A collection of image names in alphabetical order (case-insensitive).
    /// </value>
    public IEnumerable<string> Names
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Images));

            return _images.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>
    /// Adds a reference image to the collection with the specified name.
    /// </summary>
    /// <param name="name">
    /// The name to associate with the image (case-insensitive).
    /// Will be normalized to lowercase.
    /// </param>
    /// <param name="image">The reference image to add.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="image"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when an image with the specified name already exists in the collection.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the collection has been disposed.
    /// </exception>
    /// <example>
    /// <code>
    /// var images = new Images();
    /// images.Add("button", ReferenceImage.FromFile("button.png"));
    /// images.Add("logo", ReferenceImage.FromFile("logo.png"));
    /// </code>
    /// </example>
    public void Add(string name, ReferenceImage image)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (image == null)
            throw new ArgumentNullException(nameof(image));
        if (_disposed)
            throw new ObjectDisposedException(nameof(Images));

        // Normalize name to lowercase for case-insensitive storage
        var normalizedName = name.ToLowerInvariant();

        if (_images.ContainsKey(normalizedName))
            throw new ArgumentException($"An image with the name '{name}' already exists in the collection.", nameof(name));

        _images.Add(normalizedName, image);
    }

    /// <summary>
    /// Removes the reference image with the specified name from the collection.
    /// </summary>
    /// <param name="name">The name of the image to remove (case-insensitive).</param>
    /// <returns>
    /// <c>true</c> if the image was successfully removed; <c>false</c> if the image was not found.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the collection has been disposed.
    /// </exception>
    /// <remarks>
    /// The removed <see cref="ReferenceImage"/> will be disposed automatically.
    /// </remarks>
    /// <example>
    /// <code>
    /// var images = new Images();
    /// images.Add("temp", ReferenceImage.FromFile("temp.png"));
    /// bool removed = images.Remove("temp"); // true
    /// bool notFound = images.Remove("missing"); // false
    /// </code>
    /// </example>
    public bool Remove(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (_disposed)
            throw new ObjectDisposedException(nameof(Images));

        if (_images.TryGetValue(name, out var image))
        {
            _images.Remove(name);
            image.Dispose(); // Dispose the removed image
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the collection contains an image with the specified name.
    /// </summary>
    /// <param name="name">The name to check (case-insensitive).</param>
    /// <returns>
    /// <c>true</c> if the collection contains an image with the specified name; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the collection has been disposed.
    /// </exception>
    /// <example>
    /// <code>
    /// var images = Images.FromDirectory("templates");
    /// if (images.Contains("button"))
    /// {
    ///     var button = images["button"];
    /// }
    /// </code>
    /// </example>
    public bool Contains(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (_disposed)
            throw new ObjectDisposedException(nameof(Images));

        return _images.ContainsKey(name);
    }

    /// <summary>
    /// Loads all reference images from the specified directory.
    /// </summary>
    /// <param name="directoryPath">
    /// The path to the directory containing image files.
    /// </param>
    /// <param name="searchPattern">
    /// The search pattern to match file names.
    /// Default is "*.png|*.jpg|*.jpeg|*.bmp" (all common image formats).
    /// You can specify a custom pattern like "button_*.png" to load only specific files.
    /// </param>
    /// <returns>
    /// A new <see cref="Images"/> collection containing all successfully loaded images.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="directoryPath"/> is null.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the specified directory does not exist.
    /// </exception>
    /// <remarks>
    /// Image Loading:
    /// - File names (without extension) become image names
    /// - Names are normalized to lowercase for case-insensitive access
    /// - Duplicate names (case-insensitive) will skip the duplicate file
    ///
    /// Error Handling:
    /// - Corrupted or invalid image files are skipped with a warning
    /// - Loading continues even if some files fail
    /// - If all files fail to load, an empty collection is returned
    ///
    /// Supported Formats:
    /// - PNG (.png)
    /// - JPEG (.jpg, .jpeg)
    /// - BMP (.bmp)
    /// </remarks>
    /// <example>
    /// <code>
    /// // Load all images from directory
    /// using var images = Images.FromDirectory(@"C:\MyApp\Templates");
    /// Console.WriteLine($"Loaded {images.Names.Count()} images");
    ///
    /// // Load only PNG files
    /// using var pngImages = Images.FromDirectory(@"C:\MyApp\Templates", "*.png");
    ///
    /// // Load files matching pattern
    /// using var buttonImages = Images.FromDirectory(@"C:\MyApp\Templates", "button_*.png");
    /// </code>
    /// </example>
    public static Images FromDirectory(string directoryPath, string searchPattern = "*.png|*.jpg|*.jpeg|*.bmp")
    {
        if (directoryPath == null)
            throw new ArgumentNullException(nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory '{directoryPath}' does not exist.");

        var images = new Images();

        // Parse search pattern (supports multiple patterns separated by |)
        var patterns = searchPattern.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (patterns.Length == 0)
            patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" };

        var loadedCount = 0;
        var skippedCount = 0;

        foreach (var pattern in patterns)
        {
            var files = Directory.GetFiles(directoryPath, pattern.Trim(), SearchOption.TopDirectoryOnly);

            foreach (var filePath in files)
            {
                try
                {
                    // Derive name from filename without extension
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var normalizedName = fileName.ToLowerInvariant();

                    // Skip if already loaded (duplicate name)
                    if (images._images.ContainsKey(normalizedName))
                    {
                        Console.WriteLine($"[Images] Skipping duplicate name: '{fileName}' from '{Path.GetFileName(filePath)}'");
                        skippedCount++;
                        continue;
                    }

                    // Load the image
                    var refImage = ReferenceImage.FromFile(filePath);
                    images._images.Add(normalizedName, refImage);
                    loadedCount++;
                }
                catch (Exception ex)
                {
                    // Log warning and continue with next file
                    Console.WriteLine($"[Images] Warning: Failed to load '{Path.GetFileName(filePath)}': {ex.Message}");
                    skippedCount++;
                }
            }
        }

        Console.WriteLine($"[Images] Loaded {loadedCount} images from '{directoryPath}' ({skippedCount} skipped)");

        return images;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="Images"/> collection.
    /// </summary>
    /// <remarks>
    /// This method disposes all <see cref="ReferenceImage"/> instances in the collection.
    /// After disposal, the collection cannot be used.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Dispose all reference images
        foreach (var image in _images.Values)
        {
            image?.Dispose();
        }

        _images.Clear();
        _disposed = true;
    }
}
