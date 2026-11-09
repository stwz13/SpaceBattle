namespace SpaceBattle.Lib;

public interface IGameHostLifecycle
{
    GameHostState State { get; }
    void BeginMove();
    void Resume();
    void MarkMoved();
    void Stop();
}
