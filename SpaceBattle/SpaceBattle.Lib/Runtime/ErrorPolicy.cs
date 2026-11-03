using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

public enum ErrorAction { Retry, Skip, StopGame, StopThread }
public interface IErrorPolicy { ErrorAction Decide(Exception error, int attempt); }
public interface ILog { void Write(string gameId, Exception error, ErrorAction action, int attempt); }

public sealed class MemoryLog(ConcurrentQueue<(string Game, Exception Error, ErrorAction Action, int Attempt)> entries) : ILog
{
    public IReadOnlyCollection<(string Game, Exception Error, ErrorAction Action, int Attempt)> Entries => entries.ToArray();
    public void Write(string gameId, Exception error, ErrorAction action, int attempt) => entries.Enqueue((gameId, error, action, attempt));
}

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

public sealed class GameExecutionStoppedException : Exception;

public sealed class ErrorPolicyCommand(string gameId, Action execute, IErrorPolicy policy, ILog log,
    Action stopGame, Action stopThread) : ICommand
{
    public void Execute() => Enumerable.Range(0, int.MaxValue).All(attempt =>
    {
        try { execute(); return false; }
        catch (GameExecutionStoppedException) { throw; }
        catch (Exception error)
        {
            var action = policy.Decide(error, attempt);
            log.Write(gameId, error, action, attempt);
            if (action == ErrorAction.Retry) return true;
            if (action == ErrorAction.StopGame) stopGame();
            if (action == ErrorAction.StopThread) stopThread();
            if (action is ErrorAction.StopGame or ErrorAction.StopThread) throw new GameExecutionStoppedException();
            return false;
        }
    });
}

public sealed class PolicyMessageInterpreter(IMessageInterpreter inner, Func<IMessage, Action, ICommand> protect) : IMessageInterpreter
{
    public ICommand Interpret(IMessage message) => protect(message, () => inner.Interpret(message).Execute());
}
