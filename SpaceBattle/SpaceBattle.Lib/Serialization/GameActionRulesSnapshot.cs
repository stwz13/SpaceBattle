using App;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

internal static class GameActionRulesSnapshot
{
    public static void Enable(GameActionRules rules)
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
                if (snapshot["extensions"] is null) snapshot["extensions"] = extensions;
                sink(snapshot.ToJsonString());
            };
            return ScopeExecution.InScope(parent, () =>
                Ioc.Resolve<ICommand>(SerializationDependencies.Snapshot, game, save));
        });
    }

    internal static void RegisterRestore(GameRuleParameterCatalog catalog) =>
        IoCRegistrar.Register(GameSnapshotExtensions.RestoreKey("actionRules"), args =>
            args is [JsonObject data] ? new CallbackCommand(() =>
                GameRulesComposition.Install(GameRulesJson.Read(JsonSerializer.SerializeToElement(data)), catalog))
                : throw new FormatException("Expected an action rules object."));
}
