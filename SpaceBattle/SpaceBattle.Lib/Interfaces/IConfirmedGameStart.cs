namespace SpaceBattle.Lib;

public interface IConfirmedGameStart
{
    string GameId { get; }
    IManagedGameHost Start(string playerId, long revision, Func<GameActionRules, IManagedGameHost> start);
}
