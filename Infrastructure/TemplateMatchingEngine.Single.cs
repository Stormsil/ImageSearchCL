using System.Drawing;
using OpenCvSharp;

namespace ImageSearchCL.Infrastructure;

internal static partial class TemplateMatchingEngine
{
    public static MatchResult? FindTemplate(Bitmap frame, Bitmap template, double confidenceThreshold)
    {
        ValidateSearchInput(frame, template);

        using var frameMat = ConvertToMat(frame);
        using var templateMat = ConvertToMat(template);

        using var result = new Mat();
        Cv2.MatchTemplate(frameMat, templateMat, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxConfidence, out _, out var maxLoc);

        if (maxConfidence < confidenceThreshold)
            return null;

        return new MatchResult(
            x: maxLoc.X,
            y: maxLoc.Y,
            width: template.Width,
            height: template.Height,
            confidence: maxConfidence);
    }
}
