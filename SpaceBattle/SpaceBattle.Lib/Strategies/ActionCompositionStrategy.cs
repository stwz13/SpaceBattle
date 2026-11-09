using App;

namespace SpaceBattle.Lib;

internal static class ActionCompositionStrategy
{
    public static ICommand Compose(string action, params object[] commandArguments)
    {
        var names = Ioc.Resolve<string[]>(action + ".Config");
        var commands = names.Select(name => Ioc.Resolve<ICommand>(name, commandArguments)).ToArray();
        return Ioc.Resolve<ICommand>("Commands.Macro", commands.Cast<object>().ToArray());
    }
}
