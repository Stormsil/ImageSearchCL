using System.Drawing;

namespace ImageSearchCL.API;

/// <summary>
/// Represents the result of a multi-template detection, extending FindResult with information
/// about which specific template was matched.
/// </summary>
/// <remarks>
/// This class is used when searching for multiple templates simultaneously using ForAny().
/// It provides critical information about which template variant was found (e.g., button_normal.png
/// vs button_disabled.png, or english_button.png vs russian_button.png).
///
/// Inherits all positional information and anchor points from FindResult.
/// </remarks>
public sealed class MultiFindResult : FindResult
{
    /// <summary>
    /// Gets the reference image that was matched during detection.
    /// </summary>
    /// <value>
    /// The ReferenceImage object that produced this detection result.
    /// </value>
    /// <remarks>
    /// This allows you to identify which template variant was found when using ForAny().
    /// For example:
    /// - Which button state: normal, hover, or disabled
    /// - Which locale variant: English, Russian, or Chinese
    /// - Which theme: light or dark
    /// </remarks>
    public ReferenceImage MatchedTemplate { get; }

    /// <summary>
    /// Gets the zero-based index of the matched template in the original ReferenceImages array.
    /// </summary>
    /// <value>
    /// Index in the array passed to ForAny() or TrackingConfiguration.
    /// </value>
    /// <remarks>
    /// Useful for switch statements or array lookups when you prefer index-based logic
    /// over comparing ReferenceImage objects.
    /// </remarks>
    public int MatchedTemplateIndex { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiFindResult"/> class.
    /// </summary>
    /// <param name="x">X-coordinate of top-left corner.</param>
    /// <param name="y">Y-coordinate of top-left corner.</param>
    /// <param name="width">Width in pixels (must be positive).</param>
    /// <param name="height">Height in pixels (must be positive).</param>
    /// <param name="confidence">Confidence score (must be between 0.0 and 1.0).</param>
    /// <param name="timestamp">Detection timestamp.</param>
    /// <param name="matchedTemplate">The reference image that was matched.</param>
    /// <param name="matchedTemplateIndex">The index of the matched template in the source array.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if width/height are non-positive, confidence is outside [0.0, 1.0] range,
    /// or matchedTemplateIndex is negative.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="matchedTemplate"/> is null.
    /// </exception>
    public MultiFindResult(
        int x,
        int y,
        int width,
        int height,
        double confidence,
        DateTime timestamp,
        ReferenceImage matchedTemplate,
        int matchedTemplateIndex)
        : base(x, y, width, height, confidence, timestamp)
    {
        if (matchedTemplate == null)
            throw new ArgumentNullException(nameof(matchedTemplate));
        if (matchedTemplateIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(matchedTemplateIndex), matchedTemplateIndex, "Template index must be non-negative.");

        MatchedTemplate = matchedTemplate;
        MatchedTemplateIndex = matchedTemplateIndex;
    }

    /// <summary>
    /// Returns a string representation of this multi-template detection result.
    /// </summary>
    /// <returns>
    /// String in format: "MultiFindResult { X=100, Y=200, W=50, H=30, Conf=0.95, Center=(125,215), TemplateIndex=0 (100x50) }"
    /// </returns>
    public override string ToString()
    {
        return $"MultiFindResult {{ X={X}, Y={Y}, W={Width}, H={Height}, Conf={Confidence:F2}, Center=({Center.X},{Center.Y}), TemplateIndex={MatchedTemplateIndex} ({MatchedTemplate.Width}x{MatchedTemplate.Height}) }}";
    }
}
