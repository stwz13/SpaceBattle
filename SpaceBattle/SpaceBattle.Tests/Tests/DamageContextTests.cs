using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class DamageContextTests : IDisposable
{
    private readonly DirectoryInfo _tables = Directory.CreateTempSubdirectory("SpaceBattle-Tables-");
    private readonly DirectoryInfo _configs = Directory.CreateTempSubdirectory("SpaceBattle-Configs-");

    public DamageContextTests()
    {
        File.WriteAllLines(Path.Combine(_tables.FullName, "Ship-Torpedo.collisions"), ["5 0 -1 0"]);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Config", "Hit.config"),
            Path.Combine(_configs.FullName, "Hit.config"));
        File.WriteAllLines(Path.Combine(_configs.FullName, "Strike.config"), ["Commands.Hit.Collision"]);

        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategiesWithAdapters([typeof(DestroyIfDeadCommand).Assembly],
            [typeof(IDamageTarget), typeof(IDamageSource)], []).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyContextActionComposition().Execute();
        new RegisterIoCDependencyActionConfigurations(_configs.FullName).Execute();
        new RegisterIoCDependencyCollisions(_tables.FullName).Execute();
        new RegisterIoCDependencyDamageContextCommand("Context.ApplyDamage", "Target", "Source").Execute();
        new RegisterIoCDependencyContextCommand("Context.DestroyIfDead",
            GeneratedNames.CommandDependency(typeof(DestroyIfDeadCommand)), "Target").Execute();
        new RegisterIoCDependencyCollisionContextCommand("Commands.Hit.Collision", "Collisions.Ship-Torpedo",
            "Target", "Source", "Hit").Execute();
    }

    public void Dispose()
    {
        _tables.Delete(true);
        _configs.Delete(true);
    }

    private static TestGameItem Ship(int x, int y, int health)
    {
        var ship = new TestGameItem();
        ship.SetProperty("Position", new Vector(x, y));
        ship.SetProperty("Velocity", new Vector(0, 0));
        ship.SetProperty("Health", health);
        ship.SetProperty("Destroyed", false);
        return ship;
    }

    private static TestGameItem Torpedo(int x, int y, int damage)
    {
        var torpedo = new TestGameItem();
        torpedo.SetProperty("Position", new Vector(x, y));
        torpedo.SetProperty("Velocity", new Vector(-1, 0));
        torpedo.SetProperty("Damage", damage);
        return torpedo;
    }

    private static ICommandContext Context(IGameItem target, IGameItem source) => new CommandContext(
        new Dictionary<string, IGameItem> { ["Target"] = target, ["Source"] = source });

    private static Lib.ICommand Action(string name, ICommandContext context) =>
        Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, name, context);

    [Fact]
    public void Hit_Reduces_Health_Of_The_Target_By_The_Damage_Of_The_Source()
    {
        var ship = Ship(10, 3, 10);

        Action("Hit", Context(ship, Torpedo(15, 3, 4))).Execute();

        Assert.Equal(6, ship.GetProperty("Health"));
        Assert.Equal(false, ship.GetProperty("Destroyed"));
    }

    [Fact]
    public void Hit_Destroys_The_Target_When_Health_Is_Spent()
    {
        var ship = Ship(10, 3, 4);

        Action("Hit", Context(ship, Torpedo(15, 3, 9))).Execute();

        Assert.Equal(0, ship.GetProperty("Health"));
        Assert.Equal(true, ship.GetProperty("Destroyed"));
    }

    [Fact]
    public void Source_Is_Not_Changed_By_A_Hit()
    {
        var torpedo = Torpedo(15, 3, 4);

        Action("Hit", Context(Ship(10, 3, 10), torpedo)).Execute();

        Assert.Equal(4, torpedo.GetProperty("Damage"));
        Assert.Equal(new Vector(15, 3), torpedo.GetProperty("Position"));
    }

    [Fact]
    public void Strike_Damages_Only_When_The_Collision_Happens()
    {
        var hit = Ship(10, 3, 10);
        var miss = Ship(10, 3, 10);

        Action("Strike", Context(hit, Torpedo(15, 3, 4))).Execute();
        Action("Strike", Context(miss, Torpedo(16, 3, 4))).Execute();

        Assert.Equal(6, hit.GetProperty("Health"));
        Assert.Equal(10, miss.GetProperty("Health"));
    }

    [Fact]
    public void Roles_Decide_Who_Is_Damaged()
    {
        var ship = Ship(10, 3, 10);
        var torpedo = Torpedo(15, 3, 4);
        torpedo.SetProperty("Health", 7);
        ship.SetProperty("Damage", 2);

        Action("Hit", Context(torpedo, ship)).Execute();

        Assert.Equal(10, ship.GetProperty("Health"));
        Assert.Equal(5, torpedo.GetProperty("Health"));
    }

    [Fact]
    public void Missing_Role_Fails_When_The_Action_Is_Created()
    {
        var context = new CommandContext(new Dictionary<string, IGameItem> { ["Target"] = Ship(10, 3, 10) });

        Assert.Throws<KeyNotFoundException>(() => Action("Hit", context));
    }

    [Fact]
    public void Anything_But_A_Context_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>("Context.ApplyDamage", Ship(0, 0, 1)));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>("Context.ApplyDamage"));
    }

    [Theory]
    [InlineData("", "Target", "Source")]
    [InlineData("Context.X", " ", "Source")]
    [InlineData("Context.X", "Target", "")]
    [InlineData("Context.X", "Target", "Target")]
    public void Registration_Rejects_Invalid_Arguments(string key, string target, string source)
    {
        Assert.Throws<ArgumentException>(() => new RegisterIoCDependencyDamageContextCommand(key, target, source));
    }
}
