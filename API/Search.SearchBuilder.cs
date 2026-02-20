namespace ImageSearchCL.API;

public static partial class Search
{
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

        public SearchBuilder WithConfidence(double threshold)
        {
            if (threshold < 0.0 || threshold > 1.0)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold,
                    "Confidence threshold must be between 0.0 and 1.0.");

            _confidenceThreshold = threshold;
            return this;
        }

        public SearchBuilder WithMovementThreshold(double threshold)
        {
            if (threshold < 0.0)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold,
                    "Movement threshold must be non-negative.");

            _movementThreshold = threshold;
            return this;
        }

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
