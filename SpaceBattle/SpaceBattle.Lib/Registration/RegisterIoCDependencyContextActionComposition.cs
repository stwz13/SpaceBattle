using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyContextActionComposition : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", CommandContextDependencies.Action, (object[] args) =>
        {
            if (args.Length != 2 || args[0] is not string action || args[1] is not ICommandContext context)
            {
                throw new ArgumentException("A context action requires an action name and an ICommandContext.");
            }
            ArgumentException.ThrowIfNullOrWhiteSpace(action);
            return ActionCompositionStrategy.Compose(action, context);
        }).Execute();
    }
}
