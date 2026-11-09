namespace SpaceBattle.Lib;

public sealed class StopThreadErrorAction(IManagedGameRegistry games, IServerThreadControl server) : IErrorAction
{
    public bool Apply(string gameId)
    {
        var owner = games.FindManaged(gameId).Thread;
        foreach (var game in games.Games.Where(game => ReferenceEquals(game.Thread, owner))) game.StopForError();
        server.StopThread(owner);
        return false;
    }
}
