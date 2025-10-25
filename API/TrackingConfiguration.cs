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
    /// </value>
    /// <remarks>
    /// This image is used as the template for OpenCV template matching.
    /// Image format requirements handled by ReferenceImage class.
    /// </remarks>
    public ReferenceImage ReferenceImage { get; }

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
        : this(referenceImage, confidenceThreshold: 0.8, movementThreshold: 5.0)
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
    {
        if (referenceImage == null)
            throw new ArgumentNullException(nameof(referenceImage));
        if (confidenceThreshold < 0.0 || confidenceThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidenceThreshold), confidenceThreshold,
                "Confidence threshold must be between 0.0 and 1.0.");
        if (movementThreshold < 0.0)
            throw new ArgumentOutOfRangeException(nameof(movementThreshold), movementThreshold,
                "Movement threshold must be non-negative.");

        ReferenceImage = referenceImage;
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
        return new TrackingConfiguration(ReferenceImage, confidenceThreshold, MovementThreshold);
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
        return new TrackingConfiguration(ReferenceImage, ConfidenceThreshold, movementThreshold);
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
}
