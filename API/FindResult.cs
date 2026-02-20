using System.Drawing;

namespace ImageSearchCL.API;

/// <summary>
/// Represents the result of a single object detection with rich positional information.
/// </summary>
public partial class FindResult : IEquatable<FindResult>
{
    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public double Confidence { get; }

    public DateTime Timestamp { get; }

    public Point TopLeft { get; }

    public Point TopCenter { get; }

    public Point TopRight { get; }

    public Point MiddleLeft { get; }

    public Point Center { get; }

    public Point MiddleRight { get; }

    public Point BottomLeft { get; }

    public Point BottomCenter { get; }

    public Point BottomRight { get; }

    public FindResult(int x, int y, int width, int height, double confidence, DateTime timestamp)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        if (confidence < 0.0 || confidence > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0.0 and 1.0.");

        X = x;
        Y = y;
        Width = width;
        Height = height;
        Confidence = confidence;
        Timestamp = timestamp;

        var halfWidth = width / 2;
        var halfHeight = height / 2;

        TopLeft = new Point(x, y);
        TopCenter = new Point(x + halfWidth, y);
        TopRight = new Point(x + width, y);

        MiddleLeft = new Point(x, y + halfHeight);
        Center = new Point(x + halfWidth, y + halfHeight);
        MiddleRight = new Point(x + width, y + halfHeight);

        BottomLeft = new Point(x, y + height);
        BottomCenter = new Point(x + halfWidth, y + height);
        BottomRight = new Point(x + width, y + height);
    }
}
