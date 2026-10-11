namespace SpaceBattle.Lib;

public class RotateCommand : ICommand
{
    private IRotating _rotating;

    public RotateCommand(IRotating rotating)
    {
        _rotating = rotating;
    }

    public void Execute()
    {
        _rotating.Direction += _rotating.AngularVelocity;
    }
}
