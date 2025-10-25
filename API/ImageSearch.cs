using System.Drawing;

namespace ImageSearchCL.API;

/// <summary>
/// Provides static methods for one-time template matching operations.
/// </summary>
/// <remarks>
/// This class provides simple, stateless image search functionality for scenarios where
/// you don't need continuous tracking. For real-time object tracking with events,
/// use the <see cref="Search"/> API instead.
///
/// Use Cases:
/// - One-time search: "Is this button visible right now?"
/// - Batch processing: Check multiple screenshots
/// - Testing: Verify UI state
/// - Simple automation: Find and click
///
/// Performance:
/// - Each call performs template matching from scratch
/// - No caching or optimization across calls
/// - For continuous tracking, use <see cref="Search.For"/> instead
///
/// Thread Safety:
/// - All methods are thread-safe
/// - Can be called from multiple threads simultaneously
/// </remarks>
public static class ImageSearch
{
    /// <summary>
    /// Finds the best match of a template image within a screenshot.
    /// </summary>
    /// <param name="templatePath">Path to the template image file.</param>
    /// <param name="screenshot">Screenshot to search within.</param>
    /// <param name="confidence">Minimum confidence threshold (0.0-1.0). Default: 0.8</param>
    /// <returns>
    /// <see cref="FindResult"/> if a match is found above the confidence threshold; otherwise, <c>null</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="templatePath"/> or <paramref name="screenshot"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the template file does not exist.
    /// </exception>
    /// <example>
    /// <code>
    /// using var screenshot = new Bitmap(1920, 1080);
    /// using (var g = Graphics.FromImage(screenshot))
    /// {
    ///     g.CopyFromScreen(0, 0, 0, 0, screenshot.Size);
    /// }
    ///
    /// var result = ImageSearch.Find("button.png", screenshot, confidence: 0.85);
    /// if (result != null)
    /// {
    ///     Console.WriteLine($"Button found at ({result.X}, {result.Y})");
    ///     Console.WriteLine($"Confidence: {result.Confidence:P1}");
    /// }
    /// </code>
    /// </example>
    public static FindResult? Find(string templatePath, Bitmap screenshot, double confidence = 0.8)
    {
        if (templatePath == null)
            throw new ArgumentNullException(nameof(templatePath));
        if (screenshot == null)
            throw new ArgumentNullException(nameof(screenshot));

        using var template = ReferenceImage.FromFile(templatePath);
        return Find(template, screenshot, confidence);
    }

    /// <summary>
    /// Finds the best match of a template image within a screenshot.
    /// </summary>
    /// <param name="template">Template image to search for.</param>
    /// <param name="screenshot">Screenshot to search within.</param>
    /// <param name="confidence">Minimum confidence threshold (0.0-1.0). Default: 0.8</param>
    /// <returns>
    /// <see cref="FindResult"/> if a match is found above the confidence threshold; otherwise, <c>null</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="template"/> or <paramref name="screenshot"/> is null.
    /// </exception>
    public static FindResult? Find(ReferenceImage template, Bitmap screenshot, double confidence = 0.8)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (screenshot == null)
            throw new ArgumentNullException(nameof(screenshot));

        var match = Infrastructure.TemplateMatchingEngine.FindTemplate(
            screenshot,
            template.Image!,
            confidence
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
    /// Finds all occurrences of a template image within a screenshot.
    /// </summary>
    /// <param name="templatePath">Path to the template image file.</param>
    /// <param name="screenshot">Screenshot to search within.</param>
    /// <param name="confidence">Minimum confidence threshold (0.0-1.0). Default: 0.8</param>
    /// <param name="overlapThreshold">Maximum allowed overlap between detections (0.0-1.0). Default: 0.5</param>
    /// <returns>
    /// List of <see cref="FindResult"/> sorted by confidence (highest first).
    /// Returns empty list if no matches found.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="templatePath"/> or <paramref name="screenshot"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the template file does not exist.
    /// </exception>
    /// <remarks>
    /// Uses Non-Maximum Suppression (NMS) to filter overlapping detections.
    /// If two detections overlap more than <paramref name="overlapThreshold"/>,
    /// only the one with higher confidence is kept.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var screenshot = new Bitmap(1920, 1080);
    /// using (var g = Graphics.FromImage(screenshot))
    /// {
    ///     g.CopyFromScreen(0, 0, 0, 0, screenshot.Size);
    /// }
    ///
    /// var results = ImageSearch.FindAll("icon.png", screenshot, confidence: 0.80);
    /// Console.WriteLine($"Found {results.Count} icons");
    ///
    /// foreach (var result in results)
    /// {
    ///     Console.WriteLine($"  - ({result.X}, {result.Y}) - {result.Confidence:P1}");
    /// }
    /// </code>
    /// </example>
    public static List<FindResult> FindAll(
        string templatePath,
        Bitmap screenshot,
        double confidence = 0.8,
        double overlapThreshold = 0.5)
    {
        if (templatePath == null)
            throw new ArgumentNullException(nameof(templatePath));
        if (screenshot == null)
            throw new ArgumentNullException(nameof(screenshot));

        using var template = ReferenceImage.FromFile(templatePath);
        return FindAll(template, screenshot, confidence, overlapThreshold);
    }

    /// <summary>
    /// Finds all occurrences of a template image within a screenshot.
    /// </summary>
    /// <param name="template">Template image to search for.</param>
    /// <param name="screenshot">Screenshot to search within.</param>
    /// <param name="confidence">Minimum confidence threshold (0.0-1.0). Default: 0.8</param>
    /// <param name="overlapThreshold">Maximum allowed overlap between detections (0.0-1.0). Default: 0.5</param>
    /// <returns>
    /// List of <see cref="FindResult"/> sorted by confidence (highest first).
    /// Returns empty list if no matches found.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="template"/> or <paramref name="screenshot"/> is null.
    /// </exception>
    /// <remarks>
    /// Uses Non-Maximum Suppression (NMS) to filter overlapping detections.
    /// If two detections overlap more than <paramref name="overlapThreshold"/>,
    /// only the one with higher confidence is kept.
    /// </remarks>
    public static List<FindResult> FindAll(
        ReferenceImage template,
        Bitmap screenshot,
        double confidence = 0.8,
        double overlapThreshold = 0.5)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (screenshot == null)
            throw new ArgumentNullException(nameof(screenshot));

        var matches = Infrastructure.TemplateMatchingEngine.FindTemplateAll(
            screenshot,
            template.Image!,
            confidence,
            overlapThreshold
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
}
