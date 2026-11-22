using System.Text.Json;
using SpaceBattle.Lib;

namespace SpaceBattle.Examples;

public sealed class RepairPlugin : ICommandPlugin
{
    public string CommandName => "Repair";

    public ICommand Create(IGameItem target, JsonElement arguments)
    {
        if (!arguments.TryGetProperty("amount", out var amount) || amount.ValueKind != JsonValueKind.Number
            || !amount.TryGetInt32(out var points) || points <= 0)
            throw new MessageFormatException("Repair requires a positive integer amount.");

        return new RepairCommand(target, points);
    }

    private sealed class RepairCommand(IGameItem target, int points) : ICommand
    {
        public void Execute() => target.SetProperty("Health", checked((int)target.GetProperty("Health") + points));
    }
}
