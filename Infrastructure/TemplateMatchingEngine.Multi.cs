using System.Drawing;
using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;

namespace ImageSearchCL.Infrastructure;

internal static partial class TemplateMatchingEngine
{
    public static List<MatchResult> FindTemplateAll(
        Bitmap frame,
        Bitmap template,
        double confidenceThreshold,
        double overlapThreshold = 0.5)
    {
        ValidateSearchInput(frame, template);

        using var frameMat = ConvertToMat(frame);
        using var templateMat = ConvertToMat(template);
        using var result = new Mat();

        Cv2.MatchTemplate(frameMat, templateMat, result, TemplateMatchModes.CCoeffNormed);

        if (result.Type() != MatType.CV_32FC1)
        {
            throw new InvalidOperationException(
                $"Match result is not in the expected 32-bit float format. Expected CV_32FC1, got {result.Type()}.");
        }

        var matches = ExtractMatchesAboveThreshold(result, confidenceThreshold);
        matches.Sort((a, b) => b.confidence.CompareTo(a.confidence));

        return ApplyNonMaximumSuppression(matches, template.Width, template.Height, overlapThreshold);
    }

    private static List<(CvPoint location, double confidence)> ExtractMatchesAboveThreshold(Mat result, double confidenceThreshold)
    {
        var matches = new List<(CvPoint location, double confidence)>();

        unsafe
        {
            var ptr = (float*)result.DataPointer;
            for (var y = 0; y < result.Rows; y++)
            {
                for (var x = 0; x < result.Cols; x++)
                {
                    var confidence = ptr[y * result.Cols + x];
                    if (confidence >= confidenceThreshold)
                    {
                        matches.Add((new CvPoint(x, y), confidence));
                    }
                }
            }
        }

        return matches;
    }

    private static List<MatchResult> ApplyNonMaximumSuppression(
        List<(CvPoint location, double confidence)> matches,
        int templateWidth,
        int templateHeight,
        double overlapThreshold)
    {
        var results = new List<MatchResult>();

        foreach (var match in matches)
        {
            var rect = new Rectangle(match.location.X, match.location.Y, templateWidth, templateHeight);

            var overlapsAccepted = false;
            foreach (var accepted in results)
            {
                var acceptedRect = new Rectangle(accepted.X, accepted.Y, accepted.Width, accepted.Height);
                if (CalculateIoU(rect, acceptedRect) > overlapThreshold)
                {
                    overlapsAccepted = true;
                    break;
                }
            }

            if (!overlapsAccepted)
            {
                results.Add(new MatchResult(
                    x: match.location.X,
                    y: match.location.Y,
                    width: templateWidth,
                    height: templateHeight,
                    confidence: match.confidence));
            }
        }

        return results;
    }

    private static double CalculateIoU(Rectangle a, Rectangle b)
    {
        var intersection = Rectangle.Intersect(a, b);
        if (intersection.IsEmpty)
            return 0.0;

        var intersectionArea = intersection.Width * intersection.Height;
        var unionArea = (a.Width * a.Height) + (b.Width * b.Height) - intersectionArea;

        return (double)intersectionArea / unionArea;
    }
}
