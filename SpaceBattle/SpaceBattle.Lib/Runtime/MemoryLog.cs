using System.Collections.Concurrent;
namespace SpaceBattle.Lib;

public sealed class MemoryLog(ConcurrentQueue<(string Game, Exception Error, ErrorAction Action, int Attempt)> entries) : ILog
{
    public IReadOnlyCollection<(string Game, Exception Error, ErrorAction Action, int Attempt)> Entries => entries.ToArray();
    public void Write(string gameId, Exception error, ErrorAction action, int attempt) => entries.Enqueue((gameId, error, action, attempt));
}
