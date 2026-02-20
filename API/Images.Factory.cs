using System.IO;

namespace ImageSearchCL.API;

public sealed partial class Images
{
    public static Images FromDirectory(string directoryPath, string searchPattern = "*.png|*.jpg|*.jpeg|*.bmp")
    {
        if (directoryPath == null)
            throw new ArgumentNullException(nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory '{directoryPath}' does not exist.");

        var images = new Images();

        var patterns = searchPattern.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (patterns.Length == 0)
            patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"];

        foreach (var pattern in patterns)
        {
            var files = Directory.GetFiles(directoryPath, pattern.Trim(), SearchOption.TopDirectoryOnly);

            foreach (var filePath in files)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var normalizedName = fileName.ToLowerInvariant();

                    if (images._images.ContainsKey(normalizedName))
                    {
                        continue;
                    }

                    var refImage = ReferenceImage.FromFile(filePath);
                    images._images.Add(normalizedName, refImage);
                }
                catch (Exception)
                {
                    // Skip file that failed to load.
                }
            }
        }

        return images;
    }
}
