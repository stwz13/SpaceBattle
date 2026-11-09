namespace SpaceBattle.Lib;

public interface IManagedGameRegistry : IGameRegistry
{
    IReadOnlyCollection<IManagedGameHost> Games { get; }
    IManagedGameHost FindManaged(string id);
    bool Contains(string id);
}
