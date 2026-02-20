namespace ImageSearchCL.API;

public sealed partial class Images
{
    public ReferenceImage this[string name]
    {
        get
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (_disposed)
                throw new ObjectDisposedException(nameof(Images));

            if (!_images.TryGetValue(name, out var image))
            {
                var availableNames = string.Join(", ", _images.Keys.OrderBy(k => k));
                throw new KeyNotFoundException(
                    $"Image '{name}' not found in collection. Available images: {(availableNames.Length > 0 ? availableNames : "(none)")}");
            }

            return image;
        }
    }

    public IEnumerable<string> Names
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Images));

            return _images.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    public void Add(string name, ReferenceImage image)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (image == null)
            throw new ArgumentNullException(nameof(image));
        if (_disposed)
            throw new ObjectDisposedException(nameof(Images));

        var normalizedName = name.ToLowerInvariant();

        if (_images.ContainsKey(normalizedName))
            throw new ArgumentException($"An image with the name '{name}' already exists in the collection.", nameof(name));

        _images.Add(normalizedName, image);
    }

    public bool Remove(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (_disposed)
            throw new ObjectDisposedException(nameof(Images));

        if (_images.TryGetValue(name, out var image))
        {
            _images.Remove(name);
            image.Dispose();
            return true;
        }

        return false;
    }

    public bool Contains(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (_disposed)
            throw new ObjectDisposedException(nameof(Images));

        return _images.ContainsKey(name);
    }
}
