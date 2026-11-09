using System.Text.Json;
namespace SpaceBattle.Lib;

public sealed class TargetItemResolver(IReadOnlyDictionary<string, string> targets) : ITargetItemResolver
{
    public string Resolve(IAuthenticatedMessage message)
    {
        if (!targets.TryGetValue(message.Command, out var target))
            throw new UnauthorizedAccessException("The command has no target rule.");
        var args = message.Args;
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(target, out var id)
            || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
            throw new MessageFormatException("The command requires an item identifier.");
        return id.GetString()!;
    }
}
