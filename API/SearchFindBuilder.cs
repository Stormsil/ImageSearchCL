using System.Drawing;

namespace ImageSearchCL.API;

/// <summary>
/// Fluent builder for one-time template matching (Find operation).
/// </summary>
/// <remarks>
/// Used to construct a Find operation with optional configuration.
/// Provides a clean, readable API for single template matching.
/// </remarks>
public sealed class SearchFindBuilder : IDisposable
{
    private readonly ReferenceImage _referenceImage;
    private readonly bool _ownsImage;
    private double? _confidence;

    internal SearchFindBuilder(string templatePath)
    {
        _referenceImage = ReferenceImage.FromFile(templatePath);
        _ownsImage = true;
    }

    internal SearchFindBuilder(ReferenceImage referenceImage, bool ownsImage)
    {
        _referenceImage = referenceImage ?? throw new ArgumentNullException(nameof(referenceImage));
        _ownsImage = ownsImage;
    }

    /// <summary>
    /// Sets the confidence threshold for this Find operation.
    /// </summary>
    /// <param name="confidence">Confidence threshold (0.0-1.0).</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if confidence is outside [0.0, 1.0] range.
    /// </exception>
    public SearchFindBuilder WithConfidence(double confidence)
    {
        if (confidence < 0.0 || confidence > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence,
                "Confidence must be between 0.0 and 1.0.");

        _confidence = confidence;
        return this;
    }

    /// <summary>
    /// Executes the Find operation on the provided screenshot.
    /// </summary>
    /// <param name="screenshot">Screenshot to search within.</param>
    /// <returns>
    /// <see cref="FindResult"/> if a match is found above the confidence threshold; otherwise, <c>null</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="screenshot"/> is null.
    /// </exception>
    public FindResult? In(Bitmap screenshot)
    {
        if (screenshot == null)
            throw new ArgumentNullException(nameof(screenshot));

        var effectiveConfidence = _confidence ?? ImageSearchConfiguration.DefaultConfidence;

        var match = Infrastructure.TemplateMatchingEngine.FindTemplate(
            screenshot,
            _referenceImage.Image!,
            effectiveConfidence
        );

        if (match == null)
            return null;

        return new FindResult(
            match.X,
            match.Y,
            match.Width,
            match.Height,
            match.Confidence,
            DateTime.UtcNow
        );
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
