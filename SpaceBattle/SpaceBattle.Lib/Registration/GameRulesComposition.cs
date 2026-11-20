using App;

namespace SpaceBattle.Lib;

public static class GameRulesComposition
{
    public static AgreedGameStarter CreateStarter(GameRuleParameterCatalog? parameters = null)
    {
        var catalog = parameters ?? GameRuleParameterCatalog.Standard;
        GameActionRulesSnapshot.RegisterRestore(catalog);
        var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Current"));
        return new AgreedGameStarter(
            (id, rules) => ScopeExecution.InScope(scope, () =>
            {
                var game = Ioc.Resolve<IManagedGame>(GameDependencies.Create, id);
                using (game.Scope.Enter()) Install(rules, catalog);
                return game;
            }),
            Ioc.Resolve<IGameLifecycle>(RuntimeDependencies.Games));
    }

    internal static void Install(GameActionRules rules, GameRuleParameterCatalog catalog)
    {
        new RegisterIoCDependencyGameActionRules(rules).Execute();
        catalog.Install(rules.Parameters);
        GameActionRulesSnapshot.Enable(rules);
    }
}
