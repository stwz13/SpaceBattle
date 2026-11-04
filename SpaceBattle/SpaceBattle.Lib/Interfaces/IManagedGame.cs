namespace SpaceBattle.Lib;

public interface IManagedGame : IGame
{
    IGameScope Scope { get; }
    bool Paused { get; set; }
}
