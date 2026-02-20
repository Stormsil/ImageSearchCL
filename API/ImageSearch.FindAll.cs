using System.Drawing;

namespace ImageSearchCL.API;

public static partial class ImageSearch
{
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
            overlapThreshold);

        var timestamp = DateTime.UtcNow;
        return matches.Select(match => new FindResult(
            match.X,
            match.Y,
            match.Width,
            match.Height,
            match.Confidence,
            timestamp)).ToList();
    }
}
