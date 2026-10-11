namespace SpaceBattle.Lib;

public class MacroCommand : ICommand
{
    ICommand[] _cmds;
    public MacroCommand(ICommand[] cmds)
    {
        _cmds = cmds;
    }

    public void Execute()
    {
        foreach (var command in _cmds)
        {
            command.Execute();
        }
    }
}
