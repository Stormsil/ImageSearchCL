namespace ImageSearchCL.API;

/// <summary>
/// Immutable configuration for an object tracking session.
/// </summary>
public sealed partial class TrackingConfiguration
{
    public double ConfidenceThreshold { get; }

    public double MovementThreshold { get; }

    public ReferenceImage ReferenceImage => ReferenceImages[0];

    public ReferenceImage[] ReferenceImages { get; }

    public TrackingConfiguration(ReferenceImage referenceImage)
        : this([referenceImage], confidenceThreshold: 0.8, movementThreshold: 5.0)
    {
    }

    public TrackingConfiguration(params ReferenceImage[] referenceImages)
        : this(referenceImages, confidenceThreshold: 0.8, movementThreshold: 5.0)
    {
    }

    public TrackingConfiguration(
        ReferenceImage referenceImage,
        double confidenceThreshold,
        double movementThreshold)
        : this([referenceImage], confidenceThreshold, movementThreshold)
    {
    }

    public TrackingConfiguration(
        ReferenceImage[] referenceImages,
        double confidenceThreshold,
        double movementThreshold)
    {
        ValidateReferenceImages(referenceImages);
        ValidateConfidenceThreshold(confidenceThreshold);
        ValidateMovementThreshold(movementThreshold);

        ReferenceImages = referenceImages;
        ConfidenceThreshold = confidenceThreshold;
        MovementThreshold = movementThreshold;
    }
}
