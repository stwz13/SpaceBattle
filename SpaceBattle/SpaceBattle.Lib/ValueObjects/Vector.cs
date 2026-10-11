namespace SpaceBattle.Lib;

public class Vector
{
    private readonly int[] _coordinates;
    private readonly IVectorFactory _factory;

    public Vector(int[] coordinates, IVectorFactory factory)
    {
        _coordinates = coordinates.ToArray();
        _factory = factory;
        Dim = _coordinates.Length;
    }

    public int Dim { get; }
    internal IEnumerable<int> Coordinates => _coordinates;

    public static Vector operator +(Vector v1, Vector v2)
    {
        return v1._factory.Add(v1, v2);
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
