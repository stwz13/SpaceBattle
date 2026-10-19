namespace SpaceBattle.Lib;

public class Vector
{
    public int[] Coordinates { get; }

    public Vector(params int[] coordinates)
    {
        Coordinates = coordinates.ToArray();
        Dim = Coordinates.Length;
    }

    public int Dim { get; }

    public static Vector operator +(Vector v1, Vector v2)
    {
        if (v1.Dim != v2.Dim)
        {
            throw new ArgumentException("Vectors must have the same dimensions");
        }
        return new Vector(v1.Coordinates.Zip(v2.Coordinates, (a, b) => a + b).ToArray());
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Vector other) return false;
        return Coordinates.SequenceEqual(other.Coordinates);
    }

    public static bool operator ==(Vector? v1, Vector? v2) => Equals(v1, v2);

    public static bool operator !=(Vector? v1, Vector? v2) => !Equals(v1, v2);

    public override int GetHashCode()
    {
        HashCode hash = default;

        foreach (var coordinate in Coordinates)
        {
            hash.Add(coordinate);
        }

        return hash.ToHashCode();
    }

}
