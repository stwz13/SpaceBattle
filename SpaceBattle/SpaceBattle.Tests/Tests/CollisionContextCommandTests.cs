using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CollisionContextCommandTests : IDisposable
{
    private readonly DirectoryInfo _tables = Directory.CreateTempSubdirectory("SpaceBattle-Tables-");
    private readonly DirectoryInfo _configs = Directory.CreateTempSubdirectory("SpaceBattle-Configs-");
    private readonly CountingCommand _consequence = new();

    public CollisionContextCommandTests()
    {
        File.WriteAllLines(Path.Combine(_tables.FullName, "Ship-Torpedo.collisions"), ["5 0 -1 0"]);
        File.WriteAllLines(Path.Combine(_configs.FullName, "Hit.config"), ["Commands.Test.Count"]);
        File.WriteAllLines(Path.Combine(_configs.FullName, "Strike.config"), ["Commands.Hit.Collision"]);

        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyContextActionComposition().Execute();
        new RegisterIoCDependencyActionConfigurations(_configs.FullName).Execute();
        new RegisterIoCDependencyCollisions(_tables.FullName).Execute();
        Register("Commands.Test.Count", _ => _consequence);
        new RegisterIoCDependencyCollisionContextCommand("Commands.Hit.Collision", "Collisions.Ship-Torpedo",
            "Ship", "Torpedo", "Hit").Execute();
    }

    public void Dispose()
    {
        _tables.Delete(true);
        _configs.Delete(true);
    }

    private sealed class CountingCommand : Lib.ICommand
    {
        public int Count { get; private set; }
        public void Execute() => Count++;
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static TestGameItem Item(int x, int y, int vx, int vy)
    {
        var item = new TestGameItem();
        item.SetProperty("Position", new Vector(x, y));
        item.SetProperty("Velocity", new Vector(vx, vy));
        return item;
    }

    private static ICommandContext Context(IGameItem ship, IGameItem torpedo) => new CommandContext(
        new Dictionary<string, IGameItem> { ["Ship"] = ship, ["Torpedo"] = torpedo });

    private static Lib.ICommand Collision(ICommandContext context) =>
        Ioc.Resolve<Lib.ICommand>("Commands.Hit.Collision", context);

    [Fact]
    public void Hit_Runs_The_Configured_Consequence_For_The_Same_Participants()
    {
        Collision(Context(Item(10, 3, 0, 0), Item(15, 3, -1, 0))).Execute();

        Assert.Equal(1, _consequence.Count);
    }

    [Theory]
    [InlineData(16, 3, -1, 0)]
    [InlineData(15, 4, -1, 0)]
    [InlineData(15, 3, 0, 0)]
    public void Miss_Runs_Nothing(int x, int y, int vx, int vy)
    {
        Collision(Context(Item(10, 3, 0, 0), Item(x, y, vx, vy))).Execute();

        Assert.Equal(0, _consequence.Count);
    }

    [Fact]
    public void Roles_Decide_Which_Participant_Is_First()
    {
        var ship = Item(10, 3, 0, 0);
        var torpedo = Item(15, 3, -1, 0);

        Collision(new CommandContext(new Dictionary<string, IGameItem> { ["Ship"] = torpedo, ["Torpedo"] = ship }))
            .Execute();

        Assert.Equal(0, _consequence.Count);
    }

    [Fact]
    public void Participants_Are_Read_When_The_Command_Is_Executed()
    {
        var torpedo = Item(30, 3, -1, 0);
        var command = Collision(Context(Item(10, 3, 0, 0), torpedo));

        command.Execute();
        torpedo.SetProperty("Position", new Vector(15, 3));
        command.Execute();

        Assert.Equal(1, _consequence.Count);
    }

    [Fact]
    public void Collision_Can_Be_A_Step_Of_A_Configured_Context_Action()
    {
        var action = Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, "Strike",
            Context(Item(10, 3, 0, 0), Item(15, 3, -1, 0)));

        Assert.Equal(0, _consequence.Count);
        action.Execute();

        Assert.Equal(1, _consequence.Count);
    }

    [Fact]
    public void Consequence_Is_Taken_From_The_Action_Config_At_Creation()
    {
        File.WriteAllLines(Path.Combine(_configs.FullName, "Hit.config"), ["Commands.Test.Count", "Commands.Test.Count"]);
        var command = Collision(Context(Item(10, 3, 0, 0), Item(15, 3, -1, 0)));

        command.Execute();

        Assert.Equal(2, _consequence.Count);
    }

    [Fact]
    public void Consequence_Receives_The_Same_Context_As_The_Collision()
    {
        ICommandContext? received = null;
        Register("Commands.Test.Capture", args =>
        {
            received = (ICommandContext)args[0];
            return _consequence;
        });
        File.WriteAllLines(Path.Combine(_configs.FullName, "Hit.config"), ["Commands.Test.Capture"]);
        var context = Context(Item(10, 3, 0, 0), Item(15, 3, -1, 0));

        Collision(context).Execute();

        Assert.Same(context, received);
        Assert.Equal(1, _consequence.Count);
    }

    [Fact]
    public void Missing_Role_Fails_When_The_Command_Is_Created()
    {
        var context = new CommandContext(new Dictionary<string, IGameItem> { ["Ship"] = Item(10, 3, 0, 0) });

        Assert.Throws<KeyNotFoundException>(() => Collision(context));
    }

    [Fact]
    public void Anything_But_A_Context_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>("Commands.Hit.Collision", Item(0, 0, 0, 0)));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>("Commands.Hit.Collision"));
    }

    [Fact]
    public void Missing_Properties_Fail_And_Do_Not_Run_The_Consequence()
    {
        var torpedo = Item(15, 3, -1, 0);
        torpedo.Properties.Remove("Velocity");
        var command = Collision(Context(Item(10, 3, 0, 0), torpedo));

        Assert.Throws<KeyNotFoundException>(command.Execute);
        Assert.Equal(0, _consequence.Count);
    }

    [Theory]
    [InlineData("", "Collisions.A", "Ship", "Torpedo", "Hit")]
    [InlineData("Commands.X", " ", "Ship", "Torpedo", "Hit")]
    [InlineData("Commands.X", "Collisions.A", "", "Torpedo", "Hit")]
    [InlineData("Commands.X", "Collisions.A", "Ship", " ", "Hit")]
    [InlineData("Commands.X", "Collisions.A", "Ship", "Torpedo", "")]
    [InlineData("Commands.X", "Collisions.A", "Ship", "Ship", "Hit")]
    public void Registration_Rejects_Invalid_Arguments(string key, string tree, string first, string second, string action)
    {
        Assert.Throws<ArgumentException>(() =>
            new RegisterIoCDependencyCollisionContextCommand(key, tree, first, second, action));
    }
}
