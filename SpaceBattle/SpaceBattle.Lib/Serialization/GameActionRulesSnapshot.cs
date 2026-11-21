using App;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

internal static class GameActionRulesSnapshot
{
    public static void Enable(GameActionRules rules, IReadOnlyList<string>? participants, IBattleProgressTracker? progress)
    {
        var parent = new GameScope(Ioc.Resolve<object>("IoC.Scope.Parent"));
        IoCRegistrar.Register(SerializationDependencies.Snapshot, args =>
        {
            if (args is not [IGame game, Action<string> sink])
                throw new ArgumentException("A snapshot requires a game and a receiver.");
            Action<string> save = json =>
            {
                var snapshot = JsonNode.Parse(json)!.AsObject();
                var extensions = snapshot["extensions"]?.AsObject() ?? new JsonObject();
                extensions["actionRules"] = JsonSerializer.SerializeToNode(new { actions = rules.Actions, parameters = rules.Parameters });
                if (participants is not null)
                    extensions["actionRules"]!["fleetParticipants"] = JsonSerializer.SerializeToNode(participants);
                if (progress is not null)
                    extensions["actionRules"]!["battleProgress"] = JsonSerializer.SerializeToNode(progress.Current);
                if (snapshot["extensions"] is null) snapshot["extensions"] = extensions;
                sink(snapshot.ToJsonString());
            };
            return ScopeExecution.InScope(parent, () =>
                Ioc.Resolve<ICommand>(SerializationDependencies.Snapshot, game, save));
        });
    }

    internal static void RegisterRestore(GameRuleParameterCatalog catalog, IReadOnlyCollection<string> contextOrders,
        BattleSimulationModule? simulation) =>
        IoCRegistrar.Register(GameSnapshotExtensions.RestoreKey("actionRules"), args =>
            args is [JsonObject data] ? new CallbackCommand(() =>
                Restore(data, catalog, contextOrders, simulation))
                : throw new FormatException("Expected an action rules object."));

    private static void Restore(JsonObject data, GameRuleParameterCatalog catalog, IReadOnlyCollection<string> contextOrders,
        BattleSimulationModule? simulation)
    {
        var rules = data.DeepClone().AsObject();
        var participants = rules["fleetParticipants"]?.Deserialize<string[]>();
        rules.Remove("fleetParticipants");
        var progress = rules["battleProgress"]?.Deserialize<BattleProgress>();
        rules.Remove("battleProgress");
        GameRulesComposition.Install(GameRulesJson.Read(JsonSerializer.SerializeToElement(rules)), catalog, participants, contextOrders,
            simulation, progress);
    }
}
