namespace SpaceBattle.Lib;

public interface IAngleFactory
{
    Angle Create(int numerator);
    Angle Add(Angle left, Angle right);
}
