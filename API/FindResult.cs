using System.Drawing;

namespace ImageSearchCL.API;

/// <summary>
/// Represents the result of a single object detection with rich positional information.
/// </summary>
/// <remarks>
/// This class embodies the "Rich Result Objects" constitutional principle by pre-computing
/// 9 anchor points to eliminate repetitive client calculations.
///
/// Coordinate System:
/// - Origin (0,0) is top-left corner of the screen/frame
/// - X increases rightward, Y increases downward
/// - All coordinates are in pixels
///
/// Anchor Points:
/// Pre-computed for common UI automation scenarios:
/// <code>
/// TopLeft -------- TopCenter -------- TopRight
///    |                 |                  |
///    |                 |                  |
/// MiddleLeft ------- Center --------- MiddleRight
///    |                 |                  |
///    |                 |                  |
/// BottomLeft ----- BottomCenter ----- BottomRight
/// </code>
///
/// Immutability:
/// All properties are read-only to ensure thread-safety and prevent accidental mutation.
/// </remarks>
public sealed class FindResult : IEquatable<FindResult>
{
    /// <summary>
    /// Gets the X-coordinate of the top-left corner of the detected object.
    /// </summary>
    /// <value>
    /// Horizontal position in pixels from the left edge of the frame.
    /// </value>
    public int X { get; }

    /// <summary>
    /// Gets the Y-coordinate of the top-left corner of the detected object.
    /// </summary>
    /// <value>
    /// Vertical position in pixels from the top edge of the frame.
    /// </value>
    public int Y { get; }

    /// <summary>
    /// Gets the width of the detected object in pixels.
    /// </summary>
    /// <value>
    /// Width in pixels, always positive.
    /// </value>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the detected object in pixels.
    /// </summary>
    /// <value>
    /// Height in pixels, always positive.
    /// </value>
    public int Height { get; }

    /// <summary>
    /// Gets the confidence score of the detection.
    /// </summary>
    /// <value>
    /// Normalized confidence value between 0.0 (no match) and 1.0 (perfect match).
    /// Typically derived from OpenCV template matching correlation coefficient.
    /// </value>
    /// <remarks>
    /// Values above the configured threshold (typically 0.8) are considered valid detections.
    /// </remarks>
    public double Confidence { get; }

    /// <summary>
    /// Gets the timestamp when this detection occurred.
    /// </summary>
    /// <value>
    /// UTC timestamp of the detection.
    /// </value>
    /// <remarks>
    /// Used for:
    /// - Latency measurement (time from frame capture to detection)
    /// - Temporal ordering of detections
    /// - Movement velocity calculations
    /// </remarks>
    public DateTime Timestamp { get; }

    // Pre-computed anchor points (Rich Result Objects principle)

    /// <summary>
    /// Gets the top-left corner anchor point.
    /// </summary>
    /// <value>
    /// Point at (X, Y).
    /// </value>
    public Point TopLeft { get; }

    /// <summary>
    /// Gets the top-center anchor point.
    /// </summary>
    /// <value>
    /// Point at (X + Width/2, Y).
    /// </value>
    public Point TopCenter { get; }

    /// <summary>
    /// Gets the top-right corner anchor point.
    /// </summary>
    /// <value>
    /// Point at (X + Width, Y).
    /// </value>
    public Point TopRight { get; }

    /// <summary>
    /// Gets the middle-left anchor point.
    /// </summary>
    /// <value>
    /// Point at (X, Y + Height/2).
    /// </value>
    public Point MiddleLeft { get; }

    /// <summary>
    /// Gets the center anchor point.
    /// </summary>
    /// <value>
    /// Point at (X + Width/2, Y + Height/2).
    /// </value>
    public Point Center { get; }

    /// <summary>
    /// Gets the middle-right anchor point.
    /// </summary>
    /// <value>
    /// Point at (X + Width, Y + Height/2).
    /// </value>
    public Point MiddleRight { get; }

    /// <summary>
    /// Gets the bottom-left corner anchor point.
    /// </summary>
    /// <value>
    /// Point at (X, Y + Height).
    /// </value>
    public Point BottomLeft { get; }

    /// <summary>
    /// Gets the bottom-center anchor point.
    /// </summary>
    /// <value>
    /// Point at (X + Width/2, Y + Height).
    /// </value>
    public Point BottomCenter { get; }

    /// <summary>
    /// Gets the bottom-right corner anchor point.
    /// </summary>
    /// <value>
    /// Point at (X + Width, Y + Height).
    /// </value>
    public Point BottomRight { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FindResult"/> class.
    /// </summary>
    /// <param name="x">X-coordinate of top-left corner.</param>
    /// <param name="y">Y-coordinate of top-left corner.</param>
    /// <param name="width">Width in pixels (must be positive).</param>
    /// <param name="height">Height in pixels (must be positive).</param>
    /// <param name="confidence">Confidence score (must be between 0.0 and 1.0).</param>
    /// <param name="timestamp">Detection timestamp.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if width/height are non-positive or confidence is outside [0.0, 1.0] range.
    /// </exception>
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

        // Pre-compute all anchor points (Rich Result Objects principle)
        int halfWidth = width / 2;
        int halfHeight = height / 2;

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

    /// <summary>
    /// Calculates the Euclidean distance from this result's center to another result's center.
    /// </summary>
    /// <param name="other">The other result to measure distance to.</param>
    /// <returns>
    /// Distance in pixels between the two centers.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="other"/> is null.
    /// </exception>
    /// <remarks>
    /// Used for movement detection and velocity calculations.
    /// Formula: sqrt((x2-x1)² + (y2-y1)²)
    /// </remarks>
    public double DistanceTo(FindResult other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        int dx = Center.X - other.Center.X;
        int dy = Center.Y - other.Center.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Determines whether this result spatially overlaps with another result.
    /// </summary>
    /// <param name="other">The other result to check overlap with.</param>
    /// <returns>
    /// True if the bounding rectangles overlap, otherwise false.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="other"/> is null.
    /// </exception>
    /// <remarks>
    /// Used for duplicate detection and spatial filtering.
    /// Two rectangles overlap if they share any interior points.
    /// </remarks>
    public bool Overlaps(FindResult other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        // No overlap if one rectangle is completely to the left/right/above/below the other
        if (X + Width <= other.X || other.X + other.Width <= X)
            return false;
        if (Y + Height <= other.Y || other.Y + other.Height <= Y)
            return false;

        return true;
    }

    /// <summary>
    /// Returns a string representation of this detection result.
    /// </summary>
    /// <returns>
    /// String in format: "FindResult { X=100, Y=200, W=50, H=30, Conf=0.95, Center=(125,215) }"
    /// </returns>
    public override string ToString()
    {
        return $"FindResult {{ X={X}, Y={Y}, W={Width}, H={Height}, Conf={Confidence:F2}, Center=({Center.X},{Center.Y}) }}";
    }

    /// <summary>
    /// Determines whether this instance is equal to another <see cref="FindResult"/>.
    /// </summary>
    /// <param name="other">The other result to compare with.</param>
    /// <returns>
    /// True if all properties are equal, otherwise false.
    /// </returns>
    /// <remarks>
    /// Two results are equal if they have identical position, size, confidence, and timestamp.
    /// </remarks>
    public bool Equals(FindResult? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        return X == other.X
            && Y == other.Y
            && Width == other.Width
            && Height == other.Height
            && Confidence.Equals(other.Confidence)
            && Timestamp.Equals(other.Timestamp);
    }

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>
    /// True if <paramref name="obj"/> is a <see cref="FindResult"/> and all properties are equal.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as FindResult);
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>
    /// Hash code combining all position, size, confidence, and timestamp values.
    /// </returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Width, Height, Confidence, Timestamp);
    }

    /// <summary>
    /// Determines whether two <see cref="FindResult"/> instances are equal.
    /// </summary>
    public static bool operator ==(FindResult? left, FindResult? right)
    {
        if (left is null)
            return right is null;
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="FindResult"/> instances are not equal.
    /// </summary>
    public static bool operator !=(FindResult? left, FindResult? right)
    {
        return !(left == right);
    }
}
