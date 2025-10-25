namespace ImageSearchCL.API;

/// <summary>
/// Factory for creating object tracking sessions.
/// </summary>
/// <remarks>
/// This interface provides the main entry point for ImageSearchCL library.
///
/// Usage Pattern:
/// 1. Obtain factory instance (e.g., via dependency injection or static property)
/// 2. Create ICaptureSession for frame source
/// 3. Create TrackingConfiguration with reference image
/// 4. Call CreateSession() to create IObjectSearch
/// 5. Subscribe to events and call Start()
///
/// Example:
/// <code>
/// // Create factory
/// var factory = ObjectSearchFactory.Instance;
///
/// // Create capture session (user-provided)
/// ICaptureSession capture = new ScreenCaptureSession();
///
/// // Load reference image
/// var refImage = ReferenceImage.FromFile("button.png");
/// var config = new TrackingConfiguration(refImage);
///
/// // Create tracking session
/// using var session = factory.CreateSession(capture, config);
/// session.Appeared += (s, e) => Console.WriteLine($"Found at {e.Center}");
/// session.Start();
/// </code>
/// </remarks>
public interface IObjectSearchFactory
{
    /// <summary>
    /// Creates a new object tracking session.
    /// </summary>
    /// <param name="captureSession">
    /// The capture session providing video frames.
    /// Ownership is NOT transferred (caller remains responsible for disposal).
    /// </param>
    /// <param name="configuration">
    /// The tracking configuration (thresholds, reference image).
    /// Configuration is immutable and cannot be changed after session creation.
    /// </param>
    /// <returns>
    /// New IObjectSearch instance in NotStarted state.
    /// Caller must dispose when done.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="captureSession"/> or <paramref name="configuration"/> is null.
    /// </exception>
    /// <remarks>
    /// The returned session:
    /// - Is in NotStarted state
    /// - Has SynchronizationContext captured from calling thread
    /// - Must be started with Start() to begin tracking
    /// - Must be disposed when done to release resources
    ///
    /// Thread Safety:
    /// - Can be called from any thread
    /// - Captures SynchronizationContext.Current for event marshalling
    /// - UI apps: Call from UI thread to receive events on UI thread
    /// - Console apps: Call from any thread, events will fire on background thread
    /// </remarks>
    IObjectSearch CreateSession(ICaptureSession captureSession, TrackingConfiguration configuration);
}
