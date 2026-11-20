using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyGameActionRules(GameActionRules rules) : ICommand
{
    public void Execute()
    {
        ArgumentNullException.ThrowIfNull(rules);
        var configKeys = rules.Actions.Keys.Select(action => action + ".Config").ToHashSet(StringComparer.Ordinal);
        foreach (var step in rules.Actions.Values.SelectMany(steps => steps).Distinct(StringComparer.Ordinal))
            RegisterIoCDependencyRuleCommand.Bind(step);
        foreach (var (action, steps) in rules.Actions)
        {
            var key = action + ".Config";
            var keys = steps.Select(RegisterIoCDependencyRuleCommand.Key).ToArray();
            IoCRegistrar.Register(key, _ => keys.ToArray());
            var alias = "Specs." + action;
            if (!configKeys.Contains(alias))
                IoCRegistrar.Register(alias, _ => Ioc.Resolve<string[]>(key));
        }
    }
}
