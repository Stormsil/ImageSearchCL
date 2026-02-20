namespace ImageSearchCL.API;

public partial class FindResult
{
    public double DistanceTo(FindResult other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        var dx = Center.X - other.Center.X;
        var dy = Center.Y - other.Center.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public bool Overlaps(FindResult other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        if (X + Width <= other.X || other.X + other.Width <= X)
            return false;
        if (Y + Height <= other.Y || other.Y + other.Height <= Y)
            return false;

        return true;
    }
}
