namespace SpaceBattle.Lib;

public interface IAuthenticatedMessage : IMessage
{
    string PlayerId { get; }
}
