using System.Drawing;

namespace ImageSearchCL.API;

/// <summary>
/// Static facade providing convenient entry points for object tracking.
/// </summary>
/// <remarks>
/// This class provides simplified, fluent API for creating tracking sessions.
///
/// Usage patterns:
/// <code>
/// // Pattern 1: File path
/// var session = Search.For("button.png").In(captureSession);
///
/// // Pattern 2: ReferenceImage
/// using var refImage = ReferenceImage.FromFile("logo.png");
/// var session = Search.For(refImage).In(captureSession);
///
/// // Pattern 3: Bitmap
/// using var bitmap = new Bitmap("icon.png");
/// var session = Search.For(bitmap).In(captureSession);
///
/// // Pattern 4: With configuration
/// var session = Search.For("button.png")
///     .WithConfidence(0.9)
///     .WithMovementThreshold(10.0)
///     .In(captureSession);
/// </code>
///
/// All methods use ObjectSearchFactory internally.
/// </remarks>
public static class Search
{
    /// <summary>
    /// Begins a fluent search configuration for a reference image file.
    /// </summary>
    /// <param name="imagePath">Path to the reference image file.</param>
    /// <returns>
    /// SearchBuilder for fluent configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="imagePath"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown if file does not exist.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if image is invalid (too small, unsupported format).
    /// </exception>
    /// <example>
    /// <code>
    /// using var session = Search.For("button.png").In(captureSession);
    /// session.Appeared += (s, r) => Console.WriteLine($"Found at {r.Center}");
    /// session.Start();
    /// </code>
    /// </example>
    public static SearchBuilder For(string imagePath)
    {
        if (imagePath == null)
            throw new ArgumentNullException(nameof(imagePath));

        var refImage = ReferenceImage.FromFile(imagePath);
        return new SearchBuilder(refImage, ownsImage: true);
    }

    /// <summary>
    /// Begins a fluent search configuration for a reference image.
    /// </summary>
    /// <param name="referenceImage">The reference image to track.</param>
    /// <returns>
    /// SearchBuilder for fluent configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="referenceImage"/> is null.
    /// </exception>
    /// <remarks>
    /// The caller retains ownership of the ReferenceImage and must dispose it.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var refImage = ReferenceImage.FromFile("logo.png");
    /// using var session = Search.For(refImage).In(captureSession);
    /// session.Start();
    /// </code>
    /// </example>
    public static SearchBuilder For(ReferenceImage referenceImage)
    {
        if (referenceImage == null)
            throw new ArgumentNullException(nameof(referenceImage));

        return new SearchBuilder(referenceImage, ownsImage: false);
    }

    /// <summary>
    /// Begins a fluent search configuration for a bitmap.
    /// </summary>
    /// <param name="bitmap">The bitmap to use as reference image.</param>
    /// <returns>
    /// SearchBuilder for fluent configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="bitmap"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if bitmap is too small (&lt;3x3) or has unsupported format.
    /// </exception>
    /// <remarks>
    /// The Search builder takes ownership of the bitmap.
    /// Do NOT dispose the bitmap after passing it to this method.
    /// </remarks>
    /// <example>
    /// <code>
    /// var bitmap = new Bitmap("icon.png");
    /// using var session = Search.For(bitmap).In(captureSession);
    /// // Do NOT dispose bitmap - Search owns it now
    /// </code>
    /// </example>
    public static SearchBuilder For(Bitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        var refImage = new ReferenceImage(bitmap);
        return new SearchBuilder(refImage, ownsImage: true);
    }

    /// <summary>
    /// Begins a fluent search configuration for multiple reference image files (multi-template tracking).
    /// </summary>
    /// <param name="imagePaths">Array of image file paths to track (tries each one).</param>
    /// <returns>
    /// SearchBuilder for fluent configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="imagePaths"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="imagePaths"/> is empty or contains null/empty elements.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown if any file does not exist.
    /// </exception>
    /// <remarks>
    /// Template matching tries each image and returns the best match above the confidence threshold.
    ///
    /// Use cases:
    /// - UI element states: Search.ForAny("button_normal.png", "button_hover.png", "button_disabled.png")
    /// - Locale variants: Search.ForAny("en_button.png", "ru_button.png", "zh_button.png")
    /// - Theme variations: Search.ForAny("light_theme.png", "dark_theme.png")
    /// </remarks>
    /// <example>
    /// <code>
    /// using var session = Search.ForAny("button_normal.png", "button_hover.png", "button_disabled.png")
    ///     .WithConfidence(0.85)
    ///     .In(captureSession);
    ///
    /// session.Appeared += (s, r) =>
    /// {
    ///     if (r is MultiFindResult multi)
    ///     {
    ///         Console.WriteLine($"Button found in state #{multi.MatchedTemplateIndex}");
    ///     }
    /// };
    /// session.Start();
    /// </code>
    /// </example>
    public static SearchBuilder ForAny(params string[] imagePaths)
    {
        if (imagePaths == null)
            throw new ArgumentNullException(nameof(imagePaths));
        if (imagePaths.Length == 0)
            throw new ArgumentException("At least one image path is required.", nameof(imagePaths));
        if (imagePaths.Any(path => string.IsNullOrWhiteSpace(path)))
            throw new ArgumentException("Image paths array contains null or empty elements.", nameof(imagePaths));

        var referenceImages = imagePaths.Select(path => ReferenceImage.FromFile(path)).ToArray();
        return new SearchBuilder(referenceImages, ownsImages: true);
    }

    /// <summary>
    /// Begins a fluent search configuration for multiple reference images (multi-template tracking).
    /// </summary>
    /// <param name="referenceImages">Array of reference images to track (tries each one).</param>
    /// <returns>
    /// SearchBuilder for fluent configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="referenceImages"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="referenceImages"/> is empty or contains null elements.
    /// </exception>
    /// <remarks>
    /// Template matching tries each image and returns the best match above the confidence threshold.
    ///
    /// Use cases:
    /// - UI element states: Search.ForAny(normalButton, hoverButton, disabledButton)
    /// - Locale variants: Search.ForAny(enButton, ruButton, zhButton)
    /// - Theme variations: Search.ForAny(lightTheme, darkTheme)
    ///
    /// The caller retains ownership of all ReferenceImages and must dispose them.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var normalBtn = ReferenceImage.FromFile("button_normal.png");
    /// using var hoverBtn = ReferenceImage.FromFile("button_hover.png");
    /// using var disabledBtn = ReferenceImage.FromFile("button_disabled.png");
    ///
    /// using var session = Search.ForAny(normalBtn, hoverBtn, disabledBtn)
    ///     .WithConfidence(0.85)
    ///     .In(captureSession);
    ///
    /// session.Appeared += (s, r) => Console.WriteLine("Button found (any state)!");
    /// session.Start();
    /// </code>
    /// </example>
    public static SearchBuilder ForAny(params ReferenceImage[] referenceImages)
    {
        if (referenceImages == null)
            throw new ArgumentNullException(nameof(referenceImages));
        if (referenceImages.Length == 0)
            throw new ArgumentException("At least one reference image is required.", nameof(referenceImages));
        if (referenceImages.Any(img => img == null))
            throw new ArgumentException("Reference images array contains null elements.", nameof(referenceImages));

        return new SearchBuilder(referenceImages, ownsImages: false);
    }

    /// <summary>
    /// Begins a fluent one-time search for a template image file.
    /// </summary>
    /// <param name="imagePath">Path to the template image file.</param>
    /// <returns>
    /// SearchFindBuilder for fluent configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="imagePath"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown if file does not exist.
    /// </exception>
    /// <example>
    /// <code>
    /// using var screenshot = CaptureScreen();
    /// var result = Search.Find("button.png")
    ///     .WithConfidence(0.85)
    ///     .In(screenshot);
    ///
    /// if (result != null)
    /// {
    ///     Console.WriteLine($"Found at {result.Center}");
    /// }
    /// </code>
    /// </example>
    public static SearchFindBuilder Find(string imagePath)
    {
        if (imagePath == null)
            throw new ArgumentNullException(nameof(imagePath));

        return new SearchFindBuilder(imagePath);
    }

    /// <summary>
    /// Begins a fluent one-time search for a template image.
    /// </summary>
    /// <param name="referenceImage">The template to search for.</param>
    /// <returns>
    /// SearchFindBuilder for fluent configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="referenceImage"/> is null.
    /// </exception>
    /// <remarks>
    /// The caller retains ownership of the ReferenceImage and must dispose it.
    /// </remarks>
    public static SearchFindBuilder Find(ReferenceImage referenceImage)
    {
        if (referenceImage == null)
            throw new ArgumentNullException(nameof(referenceImage));

        return new SearchFindBuilder(referenceImage, ownsImage: false);
    }

    /// <summary>
    /// Begins a fluent multi-object search for a template image file.
    /// </summary>
    /// <param name="imagePath">Path to the template image file.</param>
    /// <returns>
    /// SearchFindAllBuilder for fluent configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="imagePath"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown if file does not exist.
    /// </exception>
    /// <example>
    /// <code>
    /// using var screenshot = CaptureScreen();
    /// var results = Search.FindAll("icon.png")
    ///     .WithConfidence(0.80)
    ///     .WithOverlapThreshold(0.5)
    ///     .In(screenshot);
    ///
    /// Console.WriteLine($"Found {results.Count} icons");
    /// </code>
    /// </example>
    public static SearchFindAllBuilder FindAll(string imagePath)
    {
        if (imagePath == null)
            throw new ArgumentNullException(nameof(imagePath));

        return new SearchFindAllBuilder(imagePath);
    }

    /// <summary>
    /// Begins a fluent multi-object search for a template image.
    /// </summary>
    /// <param name="referenceImage">The template to search for.</param>
    /// <returns>
    /// SearchFindAllBuilder for fluent configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="referenceImage"/> is null.
    /// </exception>
    /// <remarks>
    /// The caller retains ownership of the ReferenceImage and must dispose it.
    /// </remarks>
    public static SearchFindAllBuilder FindAll(ReferenceImage referenceImage)
    {
        if (referenceImage == null)
            throw new ArgumentNullException(nameof(referenceImage));

        return new SearchFindAllBuilder(referenceImage, ownsImage: false);
    }

    /// <summary>
    /// Fluent builder for configuring and creating tracking sessions.
    /// </summary>
    public sealed class SearchBuilder : IDisposable
    {
        private readonly ReferenceImage[] _referenceImages;
        private readonly bool _ownsImages;
        private double? _confidenceThreshold;
        private double? _movementThreshold;
        private bool _disposed;

        internal SearchBuilder(ReferenceImage referenceImage, bool ownsImage)
        {
            _referenceImages = new[] { referenceImage };
            _ownsImages = ownsImage;
        }

        internal SearchBuilder(ReferenceImage[] referenceImages, bool ownsImages)
        {
            _referenceImages = referenceImages;
            _ownsImages = ownsImages;
        }

        /// <summary>
        /// Sets the confidence threshold for template matching.
        /// </summary>
        /// <param name="threshold">
        /// Minimum confidence for detection (0.0-1.0).
        /// Higher values = stricter matching.
        /// </param>
        /// <returns>
        /// This builder for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if threshold is outside [0.0, 1.0].
        /// </exception>
        /// <example>
        /// <code>
        /// var session = Search.For("button.png")
        ///     .WithConfidence(0.95)  // Very strict
        ///     .In(captureSession);
        /// </code>
        /// </example>
        public SearchBuilder WithConfidence(double threshold)
        {
            if (threshold < 0.0 || threshold > 1.0)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold,
                    "Confidence threshold must be between 0.0 and 1.0.");

            _confidenceThreshold = threshold;
            return this;
        }

        /// <summary>
        /// Sets the movement threshold for detecting object movement.
        /// </summary>
        /// <param name="threshold">
        /// Minimum distance in pixels before Moved event fires.
        /// Higher values = less sensitive to movement.
        /// </param>
        /// <returns>
        /// This builder for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if threshold is negative.
        /// </exception>
        /// <example>
        /// <code>
        /// var session = Search.For("cursor.png")
        ///     .WithMovementThreshold(2.0)  // Very sensitive
        ///     .In(captureSession);
        /// </code>
        /// </example>
        public SearchBuilder WithMovementThreshold(double threshold)
        {
            if (threshold < 0.0)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold,
                    "Movement threshold must be non-negative.");

            _movementThreshold = threshold;
            return this;
        }

        /// <summary>
        /// Creates a tracking session with the specified capture source.
        /// </summary>
        /// <param name="captureSession">
        /// The capture session providing video frames.
        /// </param>
        /// <returns>
        /// New IObjectSearch tracking session in NotStarted state.
        /// Must be disposed when done.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="captureSession"/> is null.
        /// </exception>
        /// <remarks>
        /// After calling In(), the SearchBuilder should not be reused.
        /// Create a new builder for additional sessions.
        /// </remarks>
        /// <example>
        /// <code>
        /// using var capture = new ScreenCaptureExample();
        /// using var session = Search.For("button.png")
        ///     .WithConfidence(0.9)
        ///     .In(capture);
        ///
        /// session.Appeared += (s, r) => Console.WriteLine($"Found at {r.Center}");
        /// session.Start();
        /// </code>
        /// </example>
        public IObjectSearch In(ICaptureSession captureSession)
        {
            if (captureSession == null)
                throw new ArgumentNullException(nameof(captureSession));

            var effectiveConfidence = _confidenceThreshold ?? ImageSearchConfiguration.DefaultConfidence;
            var effectiveMovement = _movementThreshold ?? ImageSearchConfiguration.DefaultMovementThreshold;

            var config = new TrackingConfiguration(
                _referenceImages,
                effectiveConfidence,
                effectiveMovement
            );

            var factory = ObjectSearchFactory.Instance;
            return factory.CreateSession(captureSession, config);
        }

        /// <summary>
        /// Releases resources if this builder owns the reference images.
        /// </summary>
        /// <remarks>
        /// Only disposes images if they were created by For(string) or For(Bitmap).
        /// Does NOT dispose if created by For(ReferenceImage) or ForAny() - caller owns them.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_ownsImages)
            {
                foreach (var image in _referenceImages)
                {
                    image?.Dispose();
                }
            }

            _disposed = true;
        }
    }
}
