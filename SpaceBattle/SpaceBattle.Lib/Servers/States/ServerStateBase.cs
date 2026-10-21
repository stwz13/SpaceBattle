namespace SpaceBattle.Lib;

internal abstract class ServerStateBase : IServerState
{
    public abstract bool IsRunning { get; }

    public void EnsureRunning()
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("The server is not running.");
        }
    }

    public abstract void Start(IServerContext server);

    public abstract IServerThread AddThread(IServerContext server);

    public abstract Task Stop(IServerContext server, Action<IServerThread> send);
}
