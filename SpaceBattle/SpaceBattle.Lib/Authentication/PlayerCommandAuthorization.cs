namespace SpaceBattle.Lib;

public sealed class PlayerAccessOptions
{
    public string[] Commands { get; set; } = [];
    public string[] Items { get; set; } = [];
}

public interface IPlayerCommandRules
{
    void Validate(IAuthenticatedMessage message, IGameItemStorage storage);
}

public sealed class PlayerCommandRules(IReadOnlyDictionary<string, string> targets,
    IReadOnlyDictionary<string, (IReadOnlySet<string> Commands, IReadOnlySet<string> Items)> players) : IPlayerCommandRules
{
    public void Validate(IAuthenticatedMessage message, IGameItemStorage storage)
    {
        if (!players.TryGetValue(message.PlayerId, out var access) || !access.Commands.Contains(message.Command)
            || !targets.TryGetValue(message.Command, out var target))
            throw new UnauthorizedAccessException("The player cannot use this command.");
        if (message.Args.ValueKind != System.Text.Json.JsonValueKind.Object
            || !message.Args.TryGetProperty(target, out var id) || id.ValueKind != System.Text.Json.JsonValueKind.String)
            throw new MessageFormatException("The command requires an item identifier.");
        var itemId = id.GetString()!;
        if (!access.Items.Contains(itemId) && !access.Items.Contains("*"))
            throw new UnauthorizedAccessException("The player cannot access this item.");
        if (message.Command == ItemDependencies.CreateCommandKey)
        {
            if (!message.Args.TryGetProperty("properties", out var properties)
                || properties.ValueKind != System.Text.Json.JsonValueKind.Object
                || !properties.TryGetProperty("Owner", out var owner)
                || owner.ValueKind != System.Text.Json.JsonValueKind.Object
                || !owner.TryGetProperty("type", out var type) || type.ValueKind != System.Text.Json.JsonValueKind.String || type.GetString() != "string"
                || !owner.TryGetProperty("value", out var value) || value.ValueKind != System.Text.Json.JsonValueKind.String || value.GetString() != message.PlayerId)
                throw new UnauthorizedAccessException("A new item must belong to the authenticated player.");
            return;
        }
        object ownerId;
        try { ownerId = storage.Get(itemId).GetProperty("Owner"); }
        catch (KeyNotFoundException) { throw new UnauthorizedAccessException("An owned item is required."); }
        if (!Equals(ownerId, message.PlayerId))
            throw new UnauthorizedAccessException("The item belongs to another player.");
    }
}

public sealed class PlayerAuthorizationCommand(IMessage message, IMessageInterpreter interpreter,
    IPlayerCommandRules rules, Func<IGameItemStorage> storage) : ICommand
{
    public void Execute()
    {
        if (message is not IAuthenticatedMessage authenticated)
            throw new UnauthorizedAccessException("An authenticated player is required.");
        rules.Validate(authenticated, storage());
        interpreter.Interpret(message).Execute();
    }
}

public sealed class PlayerAuthorizationInterpreter(IMessageInterpreter inner,
    Func<IMessage, IMessageInterpreter, ICommand> command) : IMessageInterpreter
{
    public ICommand Interpret(IMessage message) => command(message, inner);
}
