using SpaceBattle.Lib;

namespace SpaceBattle.Tests.RotationExtensions;

public interface IRotationAuditable
{
    Angle Direction { get; }
    Angle DirectionAfterRotation { set; }
}

public class RecordRotationCommand(IRotationAuditable ship) : ICommand
{
    public void Execute() => ship.DirectionAfterRotation = ship.Direction;
}
