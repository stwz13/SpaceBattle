namespace SpaceBattle.Lib;

public class DestroyIfDeadCommand : ICommand
{
    private readonly IDestructible _item;

    public DestroyIfDeadCommand(IDestructible item)
    {
        _item = item;
    }

    public void Execute()
    {
        if (_item.Health <= 0)
        {
            _item.Destroyed = true;
        }
    }
}
