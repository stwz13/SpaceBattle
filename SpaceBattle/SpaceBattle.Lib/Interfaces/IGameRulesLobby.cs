namespace SpaceBattle.Lib;

public interface IGameRulesLobby
{
    GameRulesProposal? ForParticipant(string playerId);
    long Propose(string playerId, GameActionRules rules);
    void Confirm(string playerId, long revision);
}
