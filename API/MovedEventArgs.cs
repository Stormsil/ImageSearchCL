namespace ImageSearchCL.API;

/// <summary>
/// Event arguments for object movement detection.
/// </summary>
/// <remarks>
/// Raised when a tracked object moves beyond the configured movement threshold.
///
/// Movement Detection:
/// - Distance is measured between centers of old and new positions
/// - Only emitted if distance exceeds MovementThreshold (default: 5 pixels)
/// - Includes both old and new FindResult for complete context
///
/// Use Cases:
/// - UI automation (tracking moving UI elements)
/// - Motion analysis (calculating velocity, trajectory)
/// - Screen recording markers (highlighting moving objects)
/// - Anomaly detection (detecting unexpected movements)
/// </remarks>
public class MovedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the detection result from the previous position.
    /// </summary>
    /// <value>
    /// FindResult representing the object's previous location.
    /// </value>
    /// <remarks>
    /// Contains the object's position, size, and confidence before movement.
    /// Use OldResult.Center to get the previous center point.
    /// </remarks>
    public FindResult OldResult { get; }

    /// <summary>
    /// Gets the detection result at the new position.
    /// </summary>
    /// <value>
    /// FindResult representing the object's current location.
    /// </value>
    /// <remarks>
    /// Contains the object's position, size, and confidence after movement.
    /// Use NewResult.Center to get the new center point.
    /// </remarks>
    public FindResult NewResult { get; }

    /// <summary>
    /// Gets the distance traveled in pixels.
    /// </summary>
    /// <value>
    /// Euclidean distance between old and new center points.
    /// </value>
    /// <remarks>
    /// Calculated as: sqrt((x2-x1)² + (y2-y1)²)
    /// This value is guaranteed to be >= MovementThreshold.
    /// </remarks>
    public double Distance { get; }

    /// <summary>
    /// Gets the timestamp when the movement was detected.
    /// </summary>
    /// <value>
    /// UTC timestamp of the movement detection.
    /// </value>
    /// <remarks>
    /// Equal to NewResult.Timestamp. Provided for convenience.
    /// </remarks>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MovedEventArgs"/> class.
    /// </summary>
    /// <param name="oldResult">The previous detection result.</param>
    /// <param name="newResult">The new detection result.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="oldResult"/> or <paramref name="newResult"/> is null.
    /// </exception>
    public MovedEventArgs(FindResult oldResult, FindResult newResult)
    {
        OldResult = oldResult ?? throw new ArgumentNullException(nameof(oldResult));
        NewResult = newResult ?? throw new ArgumentNullException(nameof(newResult));
        Distance = oldResult.DistanceTo(newResult);
        Timestamp = newResult.Timestamp;
    }
}
