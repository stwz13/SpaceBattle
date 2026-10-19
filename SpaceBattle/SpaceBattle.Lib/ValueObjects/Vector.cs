namespace SpaceBattle.Lib;

public class Vector
{
    public readonly int[] _coordinates;

    public Vector(params int[] coordinates)
    {
        _coordinates = coordinates.ToArray();
        Dim = _coordinates.Length;
    }

    public int Dim { get; }

    public static Vector operator +(Vector v1, Vector v2)
    {
        if (v1.Dim != v2.Dim)
        {
            throw new ArgumentException("Vectors must have the same dimensions");
        }
        return new Vector(v1._coordinates.Zip(v2._coordinates, (a, b) => a + b).ToArray());
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Vector other) return false;
        return _coordinates.SequenceEqual(other._coordinates);
    }

    public static bool operator ==(Vector? v1, Vector? v2) => Equals(v1, v2);

    public static bool operator !=(Vector? v1, Vector? v2) => !Equals(v1, v2);

    public override int GetHashCode()
    {
        HashCode hash = default;

        foreach (var coordinate in _coordinates)
        {
            hash.Add(coordinate);
        }

        return hash.ToHashCode();
    }

}
