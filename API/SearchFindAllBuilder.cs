using System.Drawing;

namespace ImageSearchCL.API;

/// <summary>
/// Fluent builder for multi-object template matching (FindAll operation).
/// </summary>
/// <remarks>
/// Used to construct a FindAll operation with optional configuration.
/// Provides a clean, readable API for finding all occurrences of a template.
/// </remarks>
public sealed class SearchFindAllBuilder : IDisposable
{
    private readonly ReferenceImage _referenceImage;
    private readonly bool _ownsImage;
    private double? _confidence;
    private double? _overlapThreshold;

    internal SearchFindAllBuilder(string templatePath)
    {
        _referenceImage = ReferenceImage.FromFile(templatePath);
        _ownsImage = true;
    }

    internal SearchFindAllBuilder(ReferenceImage referenceImage, bool ownsImage)
    {
        _referenceImage = referenceImage ?? throw new ArgumentNullException(nameof(referenceImage));
        _ownsImage = ownsImage;
    }

    /// <summary>
    /// Sets the confidence threshold for this FindAll operation.
    /// </summary>
    /// <param name="confidence">Confidence threshold (0.0-1.0).</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if confidence is outside [0.0, 1.0] range.
    /// </exception>
    public SearchFindAllBuilder WithConfidence(double confidence)
    {
        if (confidence < 0.0 || confidence > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence,
                "Confidence must be between 0.0 and 1.0.");

        _confidence = confidence;
        return this;
    }

    /// <summary>
    /// Sets the overlap threshold for Non-Maximum Suppression.
    /// </summary>
    /// <param name="overlapThreshold">Maximum allowed overlap (0.0-1.0).</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if overlapThreshold is outside [0.0, 1.0] range.
    /// </exception>
    /// <remarks>
    /// When two detections overlap more than this threshold, only the one
    /// with higher confidence is kept.
    /// </remarks>
    public SearchFindAllBuilder WithOverlapThreshold(double overlapThreshold)
    {
        if (overlapThreshold < 0.0 || overlapThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(overlapThreshold), overlapThreshold,
                "Overlap threshold must be between 0.0 and 1.0.");

        _overlapThreshold = overlapThreshold;
        return this;
    }

    /// <summary>
    /// Executes the FindAll operation on the provided screenshot.
    /// </summary>
    /// <param name="screenshot">Screenshot to search within.</param>
    /// <returns>
    /// List of <see cref="FindResult"/> sorted by confidence (highest first).
    /// Returns empty list if no matches found.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="screenshot"/> is null.
    /// </exception>
    public List<FindResult> In(Bitmap screenshot)
    {
        if (screenshot == null)
            throw new ArgumentNullException(nameof(screenshot));

        var effectiveConfidence = _confidence ?? ImageSearchConfiguration.DefaultConfidence;
        var effectiveOverlap = _overlapThreshold ?? ImageSearchConfiguration.DefaultOverlapThreshold;

        var matches = Infrastructure.TemplateMatchingEngine.FindTemplateAll(
            screenshot,
            _referenceImage.Image!,
            effectiveConfidence,
            effectiveOverlap
        );

        var timestamp = DateTime.UtcNow;
        return matches.Select(match => new FindResult(
            match.X,
            match.Y,
            match.Width,
            match.Height,
            match.Confidence,
            timestamp
        )).ToList();
    }

    /// <summary>
    /// Releases resources used by this builder.
    /// </summary>
    public void Dispose()
    {
        if (_ownsImage)
        {
            _referenceImage?.Dispose();
        }
    }
}
