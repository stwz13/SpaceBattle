namespace SpaceBattle.Lib;

internal interface IServerThreadMembership
{
    ServerThreadMember BeginRemoval(string id);
    void RollbackRemoval(ServerThreadMember member);
    void CompleteRemoval(ServerThreadMember member);
    void EndRemoval();
}
