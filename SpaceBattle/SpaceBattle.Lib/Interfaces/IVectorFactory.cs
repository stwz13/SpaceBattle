namespace SpaceBattle.Lib;

public interface IVectorFactory
{
    Vector Create(int[] coordinates);
    Vector Add(Vector left, Vector right);
}
