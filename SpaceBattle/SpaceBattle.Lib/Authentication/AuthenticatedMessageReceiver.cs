namespace SpaceBattle.Lib;

public sealed class AuthenticatedMessageReceiver(IMessageParser parser, IGameRegistry games,
    ICommandWhitelist whitelist, PlayerIdentity player, Func<IMessage, PlayerIdentity, IMessage> authenticate) : IMessageReceiver
{
    public void Receive(string text)
    {
        var message = parser.Parse(text);
        if (message.GameId != player.GameId) throw new UnauthorizedAccessException("The token belongs to another game.");
        if (!whitelist.Allows(message.Command)) throw new MessageFormatException("The command is not allowed.");
        games.Find(message.GameId).Send(authenticate(message, player));
    }
}
