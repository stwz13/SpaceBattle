using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyWorldItemsAction : ICommand
{
    private readonly string _dependencyKey;
    private readonly string _action;
    private readonly Func<IGameItem, bool> _includes;

    public RegisterIoCDependencyWorldItemsAction(string dependencyKey, string action,
        Func<IGameItem, bool>? includes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        _dependencyKey = dependencyKey;
        _action = action;
        _includes = includes ?? (_ => true);
    }

    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", _dependencyKey, (object[] args) =>
    {
        if (args.Length != 1 || args[0] is not IGameItem frame)
            throw new ArgumentException("A world item phase requires one world step frame.");
        return Create(frame, _action, _includes);
    }).Execute();

    internal static ICommand Create(IGameItem frame, string action, Func<IGameItem, bool> includes)
    {
        var items = (IReadOnlyCollection<IGameItem>)frame.GetProperty(WorldStepDependencies.ItemsProperty);
        return new ForEachWorldItemCommand(items,
            item => Ioc.Resolve<ICommand>("Commands.Action", action, item), includes);
    }
}
