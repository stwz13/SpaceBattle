using SpaceBattle.Lib;

namespace SpaceBattle.Tests.VelocityExtensions;

public class CaptureVelocityCommand : ICommand
{
    private readonly IVelocitySnapshot _view;

    public CaptureVelocityCommand(IVelocitySnapshot view)
    {
        _view = view;
    }

    public void Execute()
    {
        _view.VelocitySnapshot = _view.Velocity;
    }
}
