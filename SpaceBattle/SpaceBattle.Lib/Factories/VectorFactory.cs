namespace SpaceBattle.Lib;

public class VectorFactory : IVectorFactory
{
    public Vector Create(int[] coordinates) => new Vector(coordinates, this);

    public Vector Add(Vector left, Vector right)
    {
        if (left.Dim != right.Dim)
        {
            throw new ArgumentException("Vectors must have the same dimensions");
        }
        return Create(left.Coordinates.Zip(right.Coordinates, (a, b) => a + b).ToArray());
    }
}
