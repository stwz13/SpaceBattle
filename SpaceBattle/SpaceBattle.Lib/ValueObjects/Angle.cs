namespace SpaceBattle.Lib;

public class Angle
{
    public static readonly int Denominator = 8;

    public int Numerator { get; }
    public double Radians { get; }

    public Angle(int numerator)
    {
        Numerator = (numerator % Denominator + Denominator) % Denominator;
        Radians = 2 * Math.PI * Numerator / Denominator;
    }

    public static Angle operator +(Angle a, Angle b)
    {
        return new Angle(a.Numerator + b.Numerator);
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
