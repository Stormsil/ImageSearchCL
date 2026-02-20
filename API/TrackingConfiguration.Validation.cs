namespace ImageSearchCL.API;

public sealed partial class TrackingConfiguration
{
    private static void ValidateReferenceImages(ReferenceImage[] referenceImages)
    {
        if (referenceImages == null)
            throw new ArgumentNullException(nameof(referenceImages));
        if (referenceImages.Length == 0)
            throw new ArgumentException("At least one reference image is required.", nameof(referenceImages));
        if (referenceImages.Any(img => img == null))
            throw new ArgumentException("Reference images array contains null elements.", nameof(referenceImages));
    }

    private static void ValidateConfidenceThreshold(double confidenceThreshold)
    {
        if (confidenceThreshold < 0.0 || confidenceThreshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidenceThreshold), confidenceThreshold,
                "Confidence threshold must be between 0.0 and 1.0.");
        }
    }

    private static void ValidateMovementThreshold(double movementThreshold)
    {
        if (movementThreshold < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(movementThreshold), movementThreshold,
                "Movement threshold must be non-negative.");
        }
    }
}
