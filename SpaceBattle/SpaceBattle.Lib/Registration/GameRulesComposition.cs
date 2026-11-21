using App;

namespace SpaceBattle.Lib;

public static class GameRulesComposition
{
    public static AgreedGameStarter CreateStarter(GameRuleParameterCatalog? parameters = null,
        IEnumerable<string>? contextOrders = null, BattleSimulationModule? simulation = null)
    {
        var catalog = parameters ?? GameRuleParameterCatalog.Standard;
        var orders = contextOrders?.ToArray() ?? [];
        GameActionRulesSnapshot.RegisterRestore(catalog, orders, simulation);
        var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Current"));
        return new AgreedGameStarter(
            (IConfirmedGameStart lobby, GameActionRules rules) => ScopeExecution.InScope(scope, () =>
            {
                var game = Ioc.Resolve<IManagedGame>(GameDependencies.Create, lobby.GameId);
                using (game.Scope.Enter()) Install(rules, catalog, (lobby as IFleetRoster)?.Participants, orders, simulation);
                return game;
            }),
            Ioc.Resolve<IGameLifecycle>(RuntimeDependencies.Games));
    }

    internal static void Install(GameActionRules rules, GameRuleParameterCatalog catalog,
        IReadOnlyList<string>? participants, IReadOnlyCollection<string> contextOrders, BattleSimulationModule? simulation,
        BattleProgress? restored = null)
    {
        new RegisterIoCDependencyGameActionRules(rules).Execute();
        catalog.Install(rules.Parameters);
        if (participants is not null)
            new RegisterIoCDependencyFleetAccess(participants, rules.Actions.Keys.Concat(contextOrders).ToArray()).Execute();
        var progress = participants is { Count: 2 } ? simulation?.Install(participants, restored) : null;
        GameActionRulesSnapshot.Enable(rules, participants, progress);
    }
}
