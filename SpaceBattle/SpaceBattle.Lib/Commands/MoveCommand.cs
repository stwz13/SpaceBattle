namespace SpaceBattle.Lib;

public class MoveCommand : ICommand
{
    private readonly IMoving _moving;

    public MoveCommand(IMoving moving)
    {
        _moving = moving;
    }

    public void Execute()
    {
        _moving.Position += _moving.Velocity;
    }
}
