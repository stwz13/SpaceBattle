using App;

namespace SpaceBattle.Lib;

internal static class ActionCompositionStrategy
{
    public static ICommand Compose(string action, params object[] commandArguments) =>
        Compose(Ioc.Resolve<string[]>(action + ".Config"), commandArguments);

    internal static ICommand Compose(IEnumerable<string> names, params object[] commandArguments)
    {
        var commands = names.Select(name => Ioc.Resolve<ICommand>(name, commandArguments)).ToArray();
        return Ioc.Resolve<ICommand>("Commands.Macro", commands.Cast<object>().ToArray());
    }
}
