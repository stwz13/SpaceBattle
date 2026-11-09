using System.Text.Json;

namespace SpaceBattle.Lib;

public class JsonMessageParser : IMessageParser
{
    private const string GameIdProperty = "gameId";
    private const string CommandProperty = "command";
    private const string ArgsProperty = "args";

    public IMessage Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var document = Read(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new MessageFormatException("A message must be a JSON object.");
        }
        var args = root.TryGetProperty(ArgsProperty, out var value) ? value.Clone() : default;
        return new Message(Text(root, GameIdProperty), Text(root, CommandProperty), args);
    }

    private static JsonDocument Read(string text)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            throw new MessageFormatException("The message is not valid JSON.", exception);
        }
    }

    private static string Text(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new MessageFormatException($"The message requires a non-empty string \"{name}\".");
        }
        return value.GetString()!;
    }
}
