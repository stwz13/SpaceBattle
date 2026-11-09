namespace SpaceBattle.Lib;

public static class ScopeExecution
{
    public static T InScope<T>(IGameScope scope, Func<T> action)
    {
        using var entered = scope.Enter();
        return action();
    }
}
