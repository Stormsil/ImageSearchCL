using ImageSearchCL.API;

namespace ImageSearchCL.WindowCapture;

/// <summary>
/// Extension methods for seamless WindowCaptureCL integration with ImageSearchCL.
/// </summary>
/// <remarks>
/// This class provides extension methods that allow using WindowCaptureCL.ICaptureSession
/// directly with ImageSearchCL's fluent API without manual adapter creation.
///
/// Usage:
/// <code>
/// // Install packages:
/// // - ImageSearchCL
/// // - WindowCaptureCL
/// // - ImageSearchCL.WindowCapture
///
/// using WindowCaptureCL;
/// using ImageSearchCL.API;
/// using ImageSearchCL.WindowCapture; // Enable extensions
///
/// var capture = Capture.FromScreen(0);
/// var session = Search.For("button.png").In(capture); // Works automatically!
/// </code>
/// </remarks>
public static class SearchBuilderExtensions
{
    /// <summary>
    /// Creates a tracking session with WindowCaptureCL as the capture source.
    /// </summary>
    /// <param name="builder">The SearchBuilder instance.</param>
    /// <param name="windowCapture">WindowCaptureCL capture session providing video frames.</param>
    /// <returns>
    /// New IObjectSearch tracking session in NotStarted state.
    /// Must be disposed when done.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="windowCapture"/> is null.
    /// </exception>
    /// <remarks>
    /// This extension method automatically wraps WindowCaptureCL.ICaptureSession
    /// into an adapter compatible with ImageSearchCL. The adapter is internal
    /// and transparent to the user.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var capture = Capture.FromScreen(0);
    /// using var session = Search.For("button.png")
    ///     .WithConfidence(0.9)
    ///     .In(capture); // Extension method called here
    ///
    /// session.Appeared += (s, r) => Console.WriteLine($"Found at {r.Center}");
    /// session.Start();
    /// </code>
    /// </example>
    public static IObjectSearch In(this Search.SearchBuilder builder, WindowCaptureCL.ICaptureSession windowCapture)
    {
        if (windowCapture == null)
            throw new ArgumentNullException(nameof(windowCapture));

        // Wrap WindowCaptureCL session with internal adapter
        var adapter = new WindowCaptureAdapter(windowCapture);

        // Use existing In() method with ImageSearchCL.API.ICaptureSession
        return builder.In(adapter);
    }
}
