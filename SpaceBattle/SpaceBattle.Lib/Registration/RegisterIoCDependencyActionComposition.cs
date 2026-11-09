using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyActionComposition : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", "Commands.Action", (object[] args) =>
            ActionCompositionStrategy.Compose((string)args[0], (IGameItem)args[1])).Execute();
    }
}
