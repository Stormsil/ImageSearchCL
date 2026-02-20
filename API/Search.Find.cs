namespace ImageSearchCL.API;

public static partial class Search
{
    public static SearchFindBuilder Find(string imagePath)
    {
        if (imagePath == null)
            throw new ArgumentNullException(nameof(imagePath));

        return new SearchFindBuilder(imagePath);
    }

    public static SearchFindBuilder Find(ReferenceImage referenceImage)
    {
        if (referenceImage == null)
            throw new ArgumentNullException(nameof(referenceImage));

        return new SearchFindBuilder(referenceImage, ownsImage: false);
    }

    public static SearchFindAllBuilder FindAll(string imagePath)
    {
        if (imagePath == null)
            throw new ArgumentNullException(nameof(imagePath));

        return new SearchFindAllBuilder(imagePath);
    }

    public static SearchFindAllBuilder FindAll(ReferenceImage referenceImage)
    {
        if (referenceImage == null)
            throw new ArgumentNullException(nameof(referenceImage));

        return new SearchFindAllBuilder(referenceImage, ownsImage: false);
    }
}
