using System.Drawing;

namespace ImageSearchCL.API;

public static partial class ImageSearch
{
    public static FindResult? Find(string templatePath, Bitmap screenshot, double confidence = 0.8)
    {
        if (templatePath == null)
            throw new ArgumentNullException(nameof(templatePath));
        if (screenshot == null)
            throw new ArgumentNullException(nameof(screenshot));

        using var template = ReferenceImage.FromFile(templatePath);
        return Find(template, screenshot, confidence);
    }

    public static FindResult? Find(ReferenceImage template, Bitmap screenshot, double confidence = 0.8)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (screenshot == null)
            throw new ArgumentNullException(nameof(screenshot));

        var match = Infrastructure.TemplateMatchingEngine.FindTemplate(
            screenshot,
            template.Image!,
            confidence);

        if (match == null)
            return null;

        return new FindResult(
            match.X,
            match.Y,
            match.Width,
            match.Height,
            match.Confidence,
            DateTime.UtcNow);
    }
}
