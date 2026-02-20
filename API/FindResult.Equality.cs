namespace ImageSearchCL.API;

public partial class FindResult
{
    public override string ToString()
    {
        return $"FindResult {{ X={X}, Y={Y}, W={Width}, H={Height}, Conf={Confidence:F2}, Center=({Center.X},{Center.Y}) }}";
    }

    public bool Equals(FindResult? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        return X == other.X
            && Y == other.Y
            && Width == other.Width
            && Height == other.Height
            && Confidence.Equals(other.Confidence)
            && Timestamp.Equals(other.Timestamp);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as FindResult);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Width, Height, Confidence, Timestamp);
    }

    public static bool operator ==(FindResult? left, FindResult? right)
    {
        if (left is null)
            return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(FindResult? left, FindResult? right)
    {
        return !(left == right);
    }
}
