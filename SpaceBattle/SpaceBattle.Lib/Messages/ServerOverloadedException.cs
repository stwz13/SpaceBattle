namespace SpaceBattle.Lib;

public class ServerOverloadedException : InvalidOperationException
{
    public ServerOverloadedException(string message) : base(message)
    {
    }
}
