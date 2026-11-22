using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class FleetOrderCommand : ICommand
{
    public const string Name = "FleetOrder";
    private readonly IAuthenticatedMessage _message;
    private readonly IFleetOrderActionFactory _actions;

    public FleetOrderCommand(IAuthenticatedMessage message, IFleetOrderActionFactory actions)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(actions);
        _message = message;
        _actions = actions;
    }

    public void Execute()
    {
        var id = RequiredField("id");
        var action = RequiredField("action");
        _actions.Create(_message.PlayerId, action, id, ReadRoles()).Execute();
    }

    private string RequiredField(string name)
    {
        if (_message.Args.ValueKind == JsonValueKind.Object && _message.Args.TryGetProperty(name, out var field) &&
            field.ValueKind == JsonValueKind.String && field.GetString() is string value && !string.IsNullOrWhiteSpace(value))
            return value;
        throw new MessageFormatException($"A fleet order requires a nonempty '{name}' field.");
    }

    private IReadOnlyDictionary<string, string> ReadRoles()
    {
        var roles = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!_message.Args.TryGetProperty("roles", out var definition)) return roles;
        if (definition.ValueKind != JsonValueKind.Object)
            throw new MessageFormatException("Fleet order roles must be an object.");

        foreach (var role in definition.EnumerateObject())
        {
            if (role.Value.ValueKind != JsonValueKind.String || role.Value.GetString() is not string id ||
                string.IsNullOrWhiteSpace(id) || !roles.TryAdd(role.Name, id))
                throw new MessageFormatException($"Role '{role.Name}' must have one nonempty item identifier.");
        }
        return roles;
    }
}
