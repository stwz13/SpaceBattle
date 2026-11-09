using SpaceBattle.Lib;

namespace SpaceBattle.Tests.VelocityExtensions;

public class CapturePairVelocityCommand(IVelocitySnapshot first, IVelocitySnapshot second) : ICommand
{
    public void Execute()
    {
        first.VelocitySnapshot = first.Velocity;
        second.VelocitySnapshot = second.Velocity;
    }
}
