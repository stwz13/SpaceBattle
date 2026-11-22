using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal static class GameRuleComposition
{
    public static AgreedGameStarter Configure(IEnumerable<RegisterIoCDependencyRuleCommand> extensions,
        GameRuleParameterCatalog parameters, IEnumerable<FleetContextOrder> orders, BattleSimulationModule simulation)
    {
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyComputedVelocity().Execute();
        new RegisterIoCDependencyProjectileFactory().Execute();
        new RegisterIoCDependencyGeneratedStrategiesWithAdapters([typeof(MoveCommand).Assembly],
            [typeof(IRefuelTarget), typeof(IRefuelSource), typeof(IDamageTarget), typeof(IDamageSource)],
            ShootingDependencies.All.Append(MovementDependencies.ComputedVelocity)).Execute();
        new RegisterIoCDependencyRefuelContextCommand("Context.Refuel", "Target", "Source").Execute();
        new RegisterIoCDependencyDamageContextCommand("Context.ApplyDamage", "Target", "Source").Execute();
        new RegisterIoCDependencyContextCommand("Context.DestroyIfDead",
            GeneratedNames.CommandDependency(typeof(DestroyIfDeadCommand)), "Target").Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyContextActionComposition().Execute();
        var fleetOrders = orders.Append(new FleetContextOrder("Refuel", "Source", ["Target"])).ToArray();
        new RegisterIoCDependencyFleetOrders(fleetOrders).Execute();
        var directory = Path.Combine(AppContext.BaseDirectory, "Config");
        new RegisterIoCDependencyActionConfigurations(directory).Execute();
        var modules = extensions.ToDictionary(extension => extension.Name, StringComparer.Ordinal);
        foreach (var step in Directory.EnumerateFiles(directory, "*.config")
            .Where(path => !string.Equals(Path.GetFileName(path), "Tick.config", StringComparison.Ordinal))
            .SelectMany(path => (string[])ActionConfigurationStrategy.Create(path)([])).Except(modules.Keys, StringComparer.Ordinal))
            RegisterIoCDependencyRuleCommand.FromExisting(step).Execute();
        foreach (var extension in modules.Values) extension.Execute();
        simulation.Configure();
        return GameRulesComposition.CreateStarter(parameters, fleetOrders.Select(order => order.Action), simulation);
    }
}
