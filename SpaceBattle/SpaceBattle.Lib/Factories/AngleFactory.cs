namespace SpaceBattle.Lib;

public class AngleFactory : IAngleFactory
{
    public Angle Create(int numerator) => new Angle(numerator, this);
    public Angle Add(Angle left, Angle right) => Create(left.Numerator + right.Numerator);
}
