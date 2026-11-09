namespace SpaceBattle.Lib;

public sealed class ErrorRecovery(string gameId, IErrorPolicy policy, ILog log,
    IReadOnlyDictionary<ErrorAction, IErrorAction> actions) : IErrorRecovery
{
    public void Execute(Action execute) => Enumerable.Range(0, int.MaxValue).All(attempt =>
    {
        try { execute(); return false; }
        catch (Exception error)
        {
            var action = policy.Decide(error, attempt);
            log.Write(gameId, error, action, attempt);
            return actions[action].Apply(gameId);
        }
    });
}
