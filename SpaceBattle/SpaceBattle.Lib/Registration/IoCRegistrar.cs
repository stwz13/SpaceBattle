using App;
namespace SpaceBattle.Lib;

internal static class IoCRegistrar
{
    public static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
