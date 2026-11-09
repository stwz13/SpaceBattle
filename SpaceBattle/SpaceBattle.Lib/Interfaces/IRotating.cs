namespace SpaceBattle.Lib;

public interface IRotating
{
    Angle Direction { get; set; }
    Angle AngularVelocity { get; }
}
