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
    /// Fluent builder for configuring and creating tracking sessions.
    /// </summary>
    public sealed class SearchBuilder : IDisposable
    {
        private readonly ReferenceImage _referenceImage;
        private readonly bool _ownsImage;
        private double _confidenceThreshold = 0.8;
        private double _movementThreshold = 5.0;
        private bool _disposed;

        internal SearchBuilder(ReferenceImage referenceImage, bool ownsImage)
        {
            _referenceImage = referenceImage;
            _ownsImage = ownsImage;
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

            var config = new TrackingConfiguration(
                _referenceImage,
                _confidenceThreshold,
                _movementThreshold
            );

            var factory = ObjectSearchFactory.Instance;
            return factory.CreateSession(captureSession, config);
        }

        /// <summary>
        /// Releases resources if this builder owns the reference image.
        /// </summary>
        /// <remarks>
        /// Only disposes the image if it was created by For(string) or For(Bitmap).
        /// Does NOT dispose if created by For(ReferenceImage) - caller owns it.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_ownsImage)
                _referenceImage?.Dispose();

            _disposed = true;
        }
    }
}
