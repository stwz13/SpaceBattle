using System.Collections.Concurrent;
namespace SpaceBattle.Lib;

public sealed class ExceptionErrorPolicy(IReadOnlyDictionary<Type, (ErrorAction Action, int Retries)> rules) : IErrorPolicy
{
    public ErrorAction Decide(Exception error, int attempt)
    {
        var rule = rules.TryGetValue(error.GetType(), out var exact) ? exact
            : rules.Where(pair => pair.Key.IsInstanceOfType(error)).Select(pair => pair.Value)
                .DefaultIfEmpty((Action: ErrorAction.Skip, Retries: 0)).First();
        return rule.Action == ErrorAction.Retry && attempt >= rule.Retries ? ErrorAction.Skip : rule.Action;
    }
}
