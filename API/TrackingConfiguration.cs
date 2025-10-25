namespace ImageSearchCL.API;

/// <summary>
/// Immutable configuration for an object tracking session.
/// </summary>
/// <remarks>
/// This class defines all tunable parameters for tracking behavior:
/// - Detection thresholds (confidence, movement)
/// - Reference image (what to track)
/// - Performance settings (future: frame skip, ROI)
///
/// Immutability Rationale:
/// - Thread-safe by design (no synchronization needed)
/// - Prevents accidental mid-session changes that could cause inconsistent behavior
/// - Enables configuration sharing across multiple sessions
/// - Simplifies testing (configurations are predictable)
///
/// To change configuration: Create a new tracking session with new configuration.
/// </remarks>
public sealed class TrackingConfiguration
{
    /// <summary>
    /// Gets the minimum confidence threshold for object detection.
    /// </summary>
    /// <value>
    /// Normalized confidence value between 0.0 and 1.0.
    /// Default: 0.8
    /// </value>
    /// <remarks>
    /// Matching Behavior:
    /// - Detections with confidence ≥ threshold are considered valid (Visible)
    /// - Detections with confidence &lt; threshold are ignored (NotVisible)
    /// - Higher values (e.g., 0.9) reduce false positives but may miss partial occlusions
    /// - Lower values (e.g., 0.7) increase sensitivity but may trigger false positives
    ///
    /// OpenCV Context:
    /// - Derived from TemplateMatchModes.CCoeffNormed result
    /// - 1.0 = perfect pixel-for-pixel match
    /// - 0.8 = typical threshold for reliable UI element matching
    /// </remarks>
    public double ConfidenceThreshold { get; }

    /// <summary>
    /// Gets the minimum distance in pixels required to trigger a Moved event.
    /// </summary>
    /// <value>
    /// Distance in pixels (must be non-negative).
    /// Default: 5
    /// </value>
    /// <remarks>
    /// Movement Detection:
    /// - Measured as Euclidean distance between old and new center points
    /// - Prevents jitter from minor template matching variations (±1-2 pixels)
    /// - Higher values (e.g., 20px) for coarse tracking (less noise)
    /// - Lower values (e.g., 2px) for precise tracking (more sensitivity)
    ///
    /// Typical Values:
    /// - UI automation: 5-10 pixels (balance between precision and noise)
    /// - High-precision tracking: 2-3 pixels (minimize latency)
    /// - Coarse tracking: 15-20 pixels (reduce event noise)
    /// </remarks>
    public double MovementThreshold { get; }

    /// <summary>
    /// Gets the reference image to track.
    /// </summary>
    /// <value>
    /// ReferenceImage containing the template and metadata.
    /// For multi-template tracking, this returns the first image.
    /// </value>
    /// <remarks>
    /// This image is used as the template for OpenCV template matching.
    /// Image format requirements handled by ReferenceImage class.
    /// </remarks>
    public ReferenceImage ReferenceImage => ReferenceImages[0];

    /// <summary>
    /// Gets all reference images for multi-template tracking.
    /// </summary>
    /// <value>
    /// Array of reference images. For single-image tracking, contains one element.
    /// </value>
    /// <remarks>
    /// When multiple images are provided, template matching tries each one
    /// and returns the best match above the confidence threshold.
    ///
    /// Use cases:
    /// - UI element states: [normal, hover, disabled]
    /// - Locale variants: [english_button, russian_button, chinese_button]
    /// - Visual variations: [light_theme, dark_theme]
    /// </remarks>
    public ReferenceImage[] ReferenceImages { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingConfiguration"/> class with default values.
    /// </summary>
    /// <param name="referenceImage">The reference image to track.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="referenceImage"/> is null.
    /// </exception>
    /// <remarks>
    /// Default values:
    /// - ConfidenceThreshold: 0.8
    /// - MovementThreshold: 5.0 pixels
    /// </remarks>
    public TrackingConfiguration(ReferenceImage referenceImage)
        : this(new[] { referenceImage }, confidenceThreshold: 0.8, movementThreshold: 5.0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingConfiguration"/> class for multi-template tracking.
    /// </summary>
    /// <param name="referenceImages">Array of reference images to track (tries each one).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="referenceImages"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="referenceImages"/> is empty or contains null elements.
    /// </exception>
    /// <remarks>
    /// Default values:
    /// - ConfidenceThreshold: 0.8
    /// - MovementThreshold: 5.0 pixels
    ///
    /// Template matching tries each image and returns the best match.
    /// </remarks>
    public TrackingConfiguration(params ReferenceImage[] referenceImages)
        : this(referenceImages, confidenceThreshold: 0.8, movementThreshold: 5.0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingConfiguration"/> class with custom values.
    /// </summary>
    /// <param name="referenceImage">The reference image to track.</param>
    /// <param name="confidenceThreshold">Minimum confidence threshold (0.0-1.0).</param>
    /// <param name="movementThreshold">Minimum movement distance in pixels (≥0).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="referenceImage"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if confidence threshold is outside [0.0, 1.0] or movement threshold is negative.
    /// </exception>
    public TrackingConfiguration(
        ReferenceImage referenceImage,
        double confidenceThreshold,
        double movementThreshold)
        : this(new[] { referenceImage }, confidenceThreshold, movementThreshold)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingConfiguration"/> class with custom values for multi-template tracking.
    /// </summary>
    /// <param name="referenceImages">Array of reference images to track.</param>
    /// <param name="confidenceThreshold">Minimum confidence threshold (0.0-1.0).</param>
    /// <param name="movementThreshold">Minimum movement distance in pixels (≥0).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="referenceImages"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="referenceImages"/> is empty or contains null elements.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if confidence threshold is outside [0.0, 1.0] or movement threshold is negative.
    /// </exception>
    public TrackingConfiguration(
        ReferenceImage[] referenceImages,
        double confidenceThreshold,
        double movementThreshold)
    {
        if (referenceImages == null)
            throw new ArgumentNullException(nameof(referenceImages));
        if (referenceImages.Length == 0)
            throw new ArgumentException("At least one reference image is required.", nameof(referenceImages));
        if (referenceImages.Any(img => img == null))
            throw new ArgumentException("Reference images array contains null elements.", nameof(referenceImages));
        if (confidenceThreshold < 0.0 || confidenceThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidenceThreshold), confidenceThreshold,
                "Confidence threshold must be between 0.0 and 1.0.");
        if (movementThreshold < 0.0)
            throw new ArgumentOutOfRangeException(nameof(movementThreshold), movementThreshold,
                "Movement threshold must be non-negative.");

        ReferenceImages = referenceImages;
        ConfidenceThreshold = confidenceThreshold;
        MovementThreshold = movementThreshold;
    }

    /// <summary>
    /// Creates a new configuration with a different confidence threshold.
    /// </summary>
    /// <param name="confidenceThreshold">New confidence threshold (0.0-1.0).</param>
    /// <returns>
    /// New TrackingConfiguration instance with updated threshold.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if confidence threshold is outside [0.0, 1.0].
    /// </exception>
    /// <remarks>
    /// Immutability helper: Creates new instance instead of modifying existing.
    /// </remarks>
    public TrackingConfiguration WithConfidenceThreshold(double confidenceThreshold)
    {
        return new TrackingConfiguration(ReferenceImages, confidenceThreshold, MovementThreshold);
    }

    /// <summary>
    /// Creates a new configuration with a different movement threshold.
    /// </summary>
    /// <param name="movementThreshold">New movement threshold in pixels (≥0).</param>
    /// <returns>
    /// New TrackingConfiguration instance with updated threshold.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if movement threshold is negative.
    /// </exception>
    /// <remarks>
    /// Immutability helper: Creates new instance instead of modifying existing.
    /// </remarks>
    public TrackingConfiguration WithMovementThreshold(double movementThreshold)
    {
        return new TrackingConfiguration(ReferenceImages, ConfidenceThreshold, movementThreshold);
    }

    /// <summary>
    /// Creates a new configuration with a different reference image.
    /// </summary>
    /// <param name="referenceImage">New reference image.</param>
    /// <returns>
    /// New TrackingConfiguration instance with updated reference image.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="referenceImage"/> is null.
    /// </exception>
    /// <remarks>
    /// Immutability helper: Creates new instance instead of modifying existing.
    /// </remarks>
    public TrackingConfiguration WithReferenceImage(ReferenceImage referenceImage)
    {
        return new TrackingConfiguration(referenceImage, ConfidenceThreshold, MovementThreshold);
    }

    /// <summary>
    /// Creates a new configuration with different reference images for multi-template tracking.
    /// </summary>
    /// <param name="referenceImages">New reference images array.</param>
    /// <returns>
    /// New TrackingConfiguration instance with updated reference images.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="referenceImages"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="referenceImages"/> is empty or contains null elements.
    /// </exception>
    /// <remarks>
    /// Immutability helper: Creates new instance instead of modifying existing.
    /// </remarks>
    public TrackingConfiguration WithReferenceImages(params ReferenceImage[] referenceImages)
    {
        return new TrackingConfiguration(referenceImages, ConfidenceThreshold, MovementThreshold);
    }
}
