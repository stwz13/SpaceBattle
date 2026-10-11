namespace SpaceBattle.Lib;

public class Angle
{
    public static readonly int Denominator = 8;

    private int _numerator;
    private readonly IAngleFactory _factory;
    public int Numerator { get { return _numerator; } }
    public double Radians { get; }

    public Angle(int numerator, IAngleFactory factory)
    {
        _numerator = (numerator % Denominator + Denominator) % Denominator;
        _factory = factory;
        Radians = 2 * Math.PI * Numerator / Denominator;
    }

    public static Angle operator +(Angle a, Angle b)
    {
        return a._factory.Add(a, b);
    }


    public override bool Equals(object? a)
    {
        if (a is Angle other)
        {
            return Numerator == other.Numerator;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return Numerator.GetHashCode();
    }

    public static bool operator ==(Angle a, Angle b)
    {
        return Equals(a, b);
    }

    public static bool operator !=(Angle a, Angle b)
    {
        return !Equals(a, b);
    }
}
