using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ImageSearchCL.Infrastructure;

/// <summary>
/// Low-level OpenCV template matching engine.
/// </summary>
/// <remarks>
/// This class provides stateless template matching operations using OpenCV.
///
/// Architecture Role:
/// - Infrastructure layer (lowest level, no business logic)
/// - Wraps OpenCvSharp4 library with clean API
/// - Stateless and thread-safe (can be shared across sessions)
/// - No event handling, state management, or lifecycle (pure function)
///
/// Algorithm:
/// - Uses TemplateMatchModes.CCoeffNormed (correlation coefficient, normalized)
/// - Returns confidence in range [0.0, 1.0] where 1.0 = perfect match
/// - Finds single best match location (not multi-object detection)
///
/// Performance:
/// - Typical: 30-60 FPS for 1920x1080 frame with 50x50 template
/// - Complexity: O(frame_width * frame_height * template_width * template_height)
/// - Optimizations: Grayscale conversion, ROI (region of interest) - future
///
/// Thread Safety:
/// - All methods are thread-safe (no shared mutable state)
/// - OpenCV operations are reentrant
/// - Can be called from multiple threads simultaneously
/// </remarks>
internal static class TemplateMatchingEngine
{
    /// <summary>
    /// Searches for a template image within a frame and returns the best match.
    /// </summary>
    /// <param name="frame">The frame to search within (haystack).</param>
    /// <param name="template">The template to search for (needle).</param>
    /// <param name="confidenceThreshold">Minimum confidence threshold (0.0-1.0).</param>
    /// <returns>
    /// MatchResult containing location and confidence, or null if no match above threshold.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="frame"/> or <paramref name="template"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if template is larger than frame.
    /// </exception>
    /// <remarks>
    /// Algorithm Steps:
    /// 1. Convert Bitmap → OpenCV Mat
    /// 2. Run TemplateMatchModes.CCoeffNormed
    /// 3. Find max confidence value and location
    /// 4. Return result if confidence ≥ threshold
    ///
    /// Performance:
    /// - Typical: 30-50ms for 1920x1080 frame, 50x50 template
    /// - Faster for smaller frames/templates
    /// - Slower for larger templates (linear with template area)
    /// </remarks>
    public static MatchResult? FindTemplate(Bitmap frame, Bitmap template, double confidenceThreshold)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (template.Width > frame.Width || template.Height > frame.Height)
            throw new ArgumentException(
                $"Template ({template.Width}x{template.Height}) cannot be larger than frame ({frame.Width}x{frame.Height}).");

        // Convert Bitmaps to OpenCV Mats with proper format
        using var frameMat = ConvertToMat(frame);
        using var templateMat = ConvertToMat(template);

        // Perform template matching
        using var result = new Mat();
        Cv2.MatchTemplate(frameMat, templateMat, result, TemplateMatchModes.CCoeffNormed);

        // Find the best match location
        Cv2.MinMaxLoc(result, out _, out double maxConfidence, out _, out OpenCvSharp.Point maxLoc);

        // Check if confidence meets threshold
        if (maxConfidence < confidenceThreshold)
            return null;

        // Return match result
        return new MatchResult(
            x: maxLoc.X,
            y: maxLoc.Y,
            width: template.Width,
            height: template.Height,
            confidence: maxConfidence
        );
    }

    /// <summary>
    /// Represents the result of a template matching operation.
    /// </summary>
    /// <remarks>
    /// This is a lightweight data transfer object for Infrastructure → Core communication.
    /// Core layer will convert this to FindResult (which includes anchors, timestamp, etc.).
    /// </remarks>
    internal sealed class MatchResult
    {
        /// <summary>
        /// Gets the X-coordinate of the top-left corner of the match.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the Y-coordinate of the top-left corner of the match.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Gets the width of the matched region.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height of the matched region.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the confidence score of the match.
        /// </summary>
        /// <value>
        /// Normalized confidence in range [0.0, 1.0].
        /// </value>
        public double Confidence { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MatchResult"/> class.
        /// </summary>
        public MatchResult(int x, int y, int width, int height, double confidence)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Confidence = confidence;
        }
    }

    /// <summary>
    /// Searches for all occurrences of a template image within a frame.
    /// </summary>
    /// <param name="frame">The frame to search within (haystack).</param>
    /// <param name="template">The template to search for (needle).</param>
    /// <param name="confidenceThreshold">Minimum confidence threshold (0.0-1.0).</param>
    /// <param name="overlapThreshold">Maximum allowed overlap between detections (0.0-1.0). Default: 0.5</param>
    /// <returns>
    /// List of MatchResult containing all matches above threshold, sorted by confidence (highest first).
    /// Returns empty list if no matches found.
    /// </returns>
    /// <remarks>
    /// Uses Non-Maximum Suppression (NMS) to filter overlapping detections.
    /// Two detections are considered overlapping if their IoU > overlapThreshold.
    /// </remarks>
    public static List<MatchResult> FindTemplateAll(
        Bitmap frame,
        Bitmap template,
        double confidenceThreshold,
        double overlapThreshold = 0.5)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (template.Width > frame.Width || template.Height > frame.Height)
            throw new ArgumentException(
                $"Template ({template.Width}x{template.Height}) cannot be larger than frame ({frame.Width}x{frame.Height}).");

        using var frameMat = ConvertToMat(frame);
        using var templateMat = ConvertToMat(template);
        using var result = new Mat();

        Cv2.MatchTemplate(frameMat, templateMat, result, TemplateMatchModes.CCoeffNormed);

        // Validate Mat type before unsafe code (CCoeffNormed always returns CV_32FC1, but verify for safety)
        if (result.Type() != MatType.CV_32FC1)
        {
            throw new InvalidOperationException(
                $"Match result is not in the expected 32-bit float format. Expected CV_32FC1, got {result.Type()}.");
        }

        // Find all matches above threshold
        var matches = new List<(OpenCvSharp.Point location, double confidence)>();

        unsafe
        {
            var ptr = (float*)result.DataPointer;
            for (int y = 0; y < result.Rows; y++)
            {
                for (int x = 0; x < result.Cols; x++)
                {
                    float confidence = ptr[y * result.Cols + x];
                    if (confidence >= confidenceThreshold)
                    {
                        matches.Add((new OpenCvSharp.Point(x, y), confidence));
                    }
                }
            }
        }

        // Sort by confidence descending
        matches.Sort((a, b) => b.confidence.CompareTo(a.confidence));

        // Apply Non-Maximum Suppression
        var results = new List<MatchResult>();
        foreach (var match in matches)
        {
            var rect = new Rectangle(match.location.X, match.location.Y, template.Width, template.Height);

            // Check if this match overlaps significantly with any accepted match
            bool overlaps = false;
            foreach (var accepted in results)
            {
                var acceptedRect = new Rectangle(accepted.X, accepted.Y, accepted.Width, accepted.Height);
                if (CalculateIoU(rect, acceptedRect) > overlapThreshold)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                results.Add(new MatchResult(
                    x: match.location.X,
                    y: match.location.Y,
                    width: template.Width,
                    height: template.Height,
                    confidence: match.confidence
                ));
            }
        }

        return results;
    }

    /// <summary>
    /// Calculates Intersection over Union (IoU) between two rectangles.
    /// </summary>
    private static double CalculateIoU(Rectangle a, Rectangle b)
    {
        var intersection = Rectangle.Intersect(a, b);
        if (intersection.IsEmpty)
            return 0.0;

        int intersectionArea = intersection.Width * intersection.Height;
        int unionArea = (a.Width * a.Height) + (b.Width * b.Height) - intersectionArea;

        return (double)intersectionArea / unionArea;
    }

    /// <summary>
    /// Converts Bitmap to Mat with proper format handling
    /// </summary>
    private static Mat ConvertToMat(Bitmap bitmap)
    {
        // Ensure bitmap is in a supported format
        if (bitmap.PixelFormat != PixelFormat.Format24bppRgb &&
            bitmap.PixelFormat != PixelFormat.Format32bppArgb &&
            bitmap.PixelFormat != PixelFormat.Format32bppRgb)
        {
            // Convert to Format24bppRgb
            var converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(converted))
            {
                g.DrawImage(bitmap, 0, 0);
            }
            return BitmapConverter.ToMat(converted);
        }

        return BitmapConverter.ToMat(bitmap);
    }
}
