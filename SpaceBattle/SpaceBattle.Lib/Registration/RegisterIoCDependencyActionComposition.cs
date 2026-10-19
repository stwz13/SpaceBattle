using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyActionComposition : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", "Commands.Action", (object[] args) =>
        {
            var action = (string)args[0];
            var item = (IGameItem)args[1];
            return ActionCompositionStrategy.Compose(action, item);
        }).Execute();
    }
}
