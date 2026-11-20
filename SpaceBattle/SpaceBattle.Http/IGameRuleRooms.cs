using SpaceBattle.Lib;

namespace SpaceBattle.Http;

public interface IGameRuleRooms
{
    void Create(string id, string player, string[] participants);
    IGameRulesLobby Find(string id);
    void Start(string id, string player, long revision);
}
