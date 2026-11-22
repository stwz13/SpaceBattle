using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class RuleCommandContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Replacement_Preserves_Move_Contract(bool useReplacement)
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(MoveCommand).Assembly, []).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        var step = GeneratedNames.CommandDependency(typeof(MoveCommand));
        if (useReplacement)
            RegisterIoCDependencyRuleCommand.For<IGameItem, IMoving>(step,
                item => Ioc.Resolve<IMoving>(GeneratedNames.AdapterDependency(typeof(IMoving)), item),
                new MoveFactory()).Execute();
        else
            RegisterIoCDependencyRuleCommand.FromExisting(step).Execute();
        new RegisterIoCDependencyGameActionRules(new GameActionRules(new Dictionary<string, string[]>
            { ["Movement"] = [step] })).Execute();

        var ship = new GameItem(new Dictionary<string, object>
        {
            ["Position"] = new Vector(0, 1), ["Velocity"] = new Vector(-1, 2), ["Fuel"] = 7
        });
        var move = Ioc.Resolve<Lib.ICommand>("Commands.Action", "Movement", ship);
        Assert.Equal(new Vector(0, 1), ship.GetProperty("Position"));
        move.Execute();
        Assert.Equal(new Vector(-1, 3), ship.GetProperty("Position"));
        ship.SetProperty("Velocity", new Vector(2, -1));
        move.Execute();
        Assert.Equal(new Vector(1, 2), ship.GetProperty("Position"));
        Assert.Equal(7, ship.GetProperty("Fuel"));

        var incomplete = new GameItem(new Dictionary<string, object> { ["Position"] = new Vector(1, 2) });
        var invalid = Ioc.Resolve<Lib.ICommand>("Commands.Action", "Movement", incomplete);
        Assert.Throws<KeyNotFoundException>(invalid.Execute);
        Assert.Equal(new Vector(1, 2), incomplete.GetProperty("Position"));
    }

    private sealed class MoveFactory : IRuleCommandFactory<IMoving>
    {
        public Lib.ICommand Create(IMoving target) =>
            new CallbackCommand(() => target.Position += target.Velocity);
    }
}
