using System.Drawing;

namespace ImageSearchCL.API;

public static partial class Search
{
    public static SearchBuilder For(string imagePath)
    {
        if (imagePath == null)
            throw new ArgumentNullException(nameof(imagePath));

        var refImage = ReferenceImage.FromFile(imagePath);
        return new SearchBuilder(refImage, ownsImage: true);
    }

    public static SearchBuilder For(ReferenceImage referenceImage)
    {
        if (referenceImage == null)
            throw new ArgumentNullException(nameof(referenceImage));

        return new SearchBuilder(referenceImage, ownsImage: false);
    }

    public static SearchBuilder For(Bitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        var refImage = new ReferenceImage(bitmap);
        return new SearchBuilder(refImage, ownsImage: true);
    }

    public static SearchBuilder ForAny(params string[] imagePaths)
    {
        if (imagePaths == null)
            throw new ArgumentNullException(nameof(imagePaths));
        if (imagePaths.Length == 0)
            throw new ArgumentException("At least one image path is required.", nameof(imagePaths));
        if (imagePaths.Any(path => string.IsNullOrWhiteSpace(path)))
            throw new ArgumentException("Image paths array contains null or empty elements.", nameof(imagePaths));

        var referenceImages = imagePaths.Select(path => ReferenceImage.FromFile(path)).ToArray();
        return new SearchBuilder(referenceImages, ownsImages: true);
    }

    public static SearchBuilder ForAny(params ReferenceImage[] referenceImages)
    {
        if (referenceImages == null)
            throw new ArgumentNullException(nameof(referenceImages));
        if (referenceImages.Length == 0)
            throw new ArgumentException("At least one reference image is required.", nameof(referenceImages));
        if (referenceImages.Any(img => img == null))
            throw new ArgumentException("Reference images array contains null elements.", nameof(referenceImages));

        return new SearchBuilder(referenceImages, ownsImages: false);
    }
}
