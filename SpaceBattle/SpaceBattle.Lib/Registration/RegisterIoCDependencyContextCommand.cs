using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyContextCommand : ICommand
{
    private readonly string _dependencyKey;
    private readonly string _commandKey;
    private readonly string _role;

    public RegisterIoCDependencyContextCommand(string dependencyKey, string commandKey, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        if (dependencyKey == commandKey)
        {
            throw new ArgumentException("A context command must have a different key from its target command.",
                nameof(dependencyKey));
        }
        _dependencyKey = dependencyKey;
        _commandKey = commandKey;
        _role = role;
    }

    public void Execute()
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", _dependencyKey, (object[] args) =>
        {
            if (args.Length != 1 || args[0] is not ICommandContext context)
            {
                throw new ArgumentException("A context command requires an ICommandContext.");
            }
            return Ioc.Resolve<ICommand>(_commandKey, context.GetItem(_role));
        }).Execute();
    }
}
