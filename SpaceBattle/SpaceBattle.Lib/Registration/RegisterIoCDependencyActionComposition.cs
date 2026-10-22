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
            var names = Ioc.Resolve<string[]>(action + ".Config");
            var commands = names.Select(name => Ioc.Resolve<ICommand>(name, item)).ToArray();
            return Ioc.Resolve<ICommand>("Commands.Macro", commands.Cast<object>().ToArray());
        }).Execute();
    }
}
