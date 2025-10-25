using ImageSearchCL.Core;

namespace ImageSearchCL.API;

/// <summary>
/// Default implementation of <see cref="IObjectSearchFactory"/>.
/// </summary>
/// <remarks>
/// This class provides a singleton instance for convenient access.
///
/// Usage:
/// <code>
/// var factory = ObjectSearchFactory.Instance;
/// var session = factory.CreateSession(captureSession, configuration);
/// </code>
///
/// Alternatively, create instances directly for dependency injection:
/// <code>
/// services.AddSingleton&lt;IObjectSearchFactory, ObjectSearchFactory&gt;();
/// </code>
/// </remarks>
public sealed class ObjectSearchFactory : IObjectSearchFactory
{
    private static readonly Lazy<ObjectSearchFactory> _instance =
        new Lazy<ObjectSearchFactory>(() => new ObjectSearchFactory());

    /// <summary>
    /// Gets the singleton instance of the factory.
    /// </summary>
    /// <value>
    /// Thread-safe singleton instance.
    /// </value>
    public static ObjectSearchFactory Instance => _instance.Value;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectSearchFactory"/> class.
    /// </summary>
    /// <remarks>
    /// Public constructor for dependency injection scenarios.
    /// For simple usage, prefer the static <see cref="Instance"/> property.
    /// </remarks>
    public ObjectSearchFactory()
    {
    }

    /// <inheritdoc/>
    public IObjectSearch CreateSession(ICaptureSession captureSession, TrackingConfiguration configuration)
    {
        if (captureSession == null)
            throw new ArgumentNullException(nameof(captureSession));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        return new TrackingSession(captureSession, configuration);
    }
}
