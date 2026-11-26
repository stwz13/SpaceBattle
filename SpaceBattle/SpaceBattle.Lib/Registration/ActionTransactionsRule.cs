using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class ActionTransactionsRule(bool enabled, IEnumerable<ITransactionCommandEffect> effects)
    : GameClassParameter<bool>("Transactions.Enabled", value => Read(value, enabled),
        value =>
        {
            if (value)
                new RegisterIoCDependencyActionTransactions(effects).Execute();
        })
{
    private static bool Read(JsonElement value, bool enabled)
    {
        var requested = value.GetBoolean();
        if (requested && !enabled)
            throw new ArgumentException("This server has not enabled action transactions.");
        return requested;
    }
}