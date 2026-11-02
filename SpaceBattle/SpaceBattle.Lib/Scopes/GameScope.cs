using App;

namespace SpaceBattle.Lib;

public class GameScope : IGameScope
{
    private readonly object _scope;

    public GameScope(object scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scope = scope;
    }

    public IDisposable Enter()
    {
        var previous = Ioc.Resolve<object>("IoC.Scope.Current");
        SetCurrent(_scope);
        return new Restore(previous);
    }

    private static void SetCurrent(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private sealed class Restore(object previous) : IDisposable
    {
        public void Dispose() => SetCurrent(previous);
    }
}
