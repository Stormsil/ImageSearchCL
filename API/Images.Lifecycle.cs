namespace ImageSearchCL.API;

public sealed partial class Images
{
    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var image in _images.Values)
        {
            image?.Dispose();
        }

        _images.Clear();
        _disposed = true;
    }
}
