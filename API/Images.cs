using System.Collections.Generic;

namespace ImageSearchCL.API;

/// <summary>
/// Manages a collection of reference images accessible by name.
/// </summary>
public sealed partial class Images : IDisposable
{
    private readonly Dictionary<string, ReferenceImage> _images;
    private bool _disposed;

    public Images()
    {
        _images = new Dictionary<string, ReferenceImage>(StringComparer.OrdinalIgnoreCase);
    }
}
