namespace SpaceBattle.Lib;

public sealed class StopCurrentThreadErrorAction(IManagedGameRegistry games, IServerThreadCatalog threads,
    IServerThreadControl server) : IErrorAction
{
    public bool Apply(string gameId)
    {
        var current = threads.Threads.Single(member => member.Thread.ThreadId == Environment.CurrentManagedThreadId).Thread;
        foreach (var game in games.Games.Where(game => ReferenceEquals(game.Thread, current))) game.StopForError();
        server.StopThread(current);
        return false;
    }
}
