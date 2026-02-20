namespace ImageSearchCL.API;

public sealed partial class TrackingConfiguration
{
    public TrackingConfiguration WithConfidenceThreshold(double confidenceThreshold)
    {
        return new TrackingConfiguration(ReferenceImages, confidenceThreshold, MovementThreshold);
    }

    public TrackingConfiguration WithMovementThreshold(double movementThreshold)
    {
        return new TrackingConfiguration(ReferenceImages, ConfidenceThreshold, movementThreshold);
    }

    public TrackingConfiguration WithReferenceImage(ReferenceImage referenceImage)
    {
        return new TrackingConfiguration(referenceImage, ConfidenceThreshold, MovementThreshold);
    }

    public TrackingConfiguration WithReferenceImages(params ReferenceImage[] referenceImages)
    {
        return new TrackingConfiguration(referenceImages, ConfidenceThreshold, MovementThreshold);
    }
}
