namespace SpaceBattle.Lib;

public class MessageReceiver : IMessageReceiver
{
    private readonly IMessageParser _parser;
    private readonly IGameRegistry _games;

    public MessageReceiver(IMessageParser parser, IGameRegistry games)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(games);
        _parser = parser;
        _games = games;
    }

    public void Receive(string text)
    {
        var message = _parser.Parse(text);
        _games.Find(message.GameId).Send(message);
    }
}
