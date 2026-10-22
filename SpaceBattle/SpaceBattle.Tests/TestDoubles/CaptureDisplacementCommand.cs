using SpaceBattle.Lib;

namespace SpaceBattle.Tests.VelocityExtensions;

public class CaptureDisplacementCommand : ICommand
{
    private readonly IDisplacementSnapshot _view;

    public CaptureDisplacementCommand(IDisplacementSnapshot view)
    {
        _view = view;
    }

    public void Execute()
    {
        _view.DisplacementSnapshot = _view.Displacement;
    }
}
