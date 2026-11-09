namespace SpaceBattle.Lib;

public sealed class StopGameErrorAction(IManagedGameRegistry games) : IErrorAction
{
    public bool Apply(string gameId) { games.FindManaged(gameId).StopForError(); return false; }
}
