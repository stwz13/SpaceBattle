using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class LongMovementTests
{
    private readonly GameScope _scope;

    public LongMovementTests()
    {
        new InitCommand().Execute();
        _scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        using var scope = _scope.Enter();
        new RegisterIoCDependencyComputedVelocity().Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(MoveCommand).Assembly,
            [MovementDependencies.ComputedVelocity]).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
        new RegisterIoCDependencyLongOperations().Execute();
    }

    [Fact]
    public void Movement_Repeats_In_Fifo_Order_Uses_Current_Heading_And_Stops()
    {
        using var scope = _scope.Enter();
        var game = CreateGame("battle", _scope);
        RegisterMovement(game);
        var item = Item(fuel: 10);
        var movement = Movement(item);
        var sameMovement = Movement(item);

        Assert.Same(movement, sameMovement);
        game.Post(movement.Start);
        game.Execute();
        var continuation = Assert.Single(game.Pending);
        Assert.Equal(new Vector(0, 0), item.GetProperty("Position"));
        game.Post(new CallbackCommand(() => item.SetProperty("Direction", new Angle(2))));
        game.Post(sameMovement.Start);

        game.Execute();
        Assert.Equal(new Vector(3, 0), item.GetProperty("Position"));
        Assert.Same(continuation, game.Pending.Last());
        game.Execute();
        game.Execute();
        Assert.Same(continuation, Assert.Single(game.Pending));
        game.Execute();
        Assert.Equal(new Vector(3, 3), item.GetProperty("Position"));

        game.Post(movement.Stop);
        game.Execute();
        game.Execute();
        game.Execute();

        Assert.Equal(new Vector(3, 6), item.GetProperty("Position"));
        Assert.Equal(4, item.GetProperty("Fuel"));
        Assert.False(game.HasCommands);
    }

    [Fact]
    public void Successful_Steps_Report_Their_Positions_And_Insufficient_Fuel_Stops_The_Operation()
    {
        using var scope = _scope.Enter();
        var game = CreateGame("battle", _scope);
        var observed = new List<(IGameItem Item, Vector Start, Vector End)>();
        RegisterMovement(game, (item, start, end) => observed.Add((item, start, end)));
        var ship = Item(fuel: 2);
        var previous = (Vector)ship.GetProperty("Position");
        var movement = Movement(ship);
        game.Post(movement.Start);
        game.Execute();
        game.Execute();

        previous.Coordinates[0] = 100;
        var segment = Assert.Single(observed);
        Assert.Same(ship, segment.Item);
        Assert.Equal(new Vector(0, 0), segment.Start);
        Assert.Equal(new Vector(3, 0), segment.End);
        Assert.NotSame(ship.GetProperty("Position"), segment.End);

        Assert.Throws<InvalidOperationException>(game.Execute);
        Assert.False(game.HasCommands);
        Assert.Single(observed);
        Assert.Equal(0, ship.GetProperty("Fuel"));
        Assert.Equal(new Vector(3, 0), ship.GetProperty("Position"));

        ship.SetProperty("Fuel", 2);
        game.Post(movement.Start);
        game.Execute();
        game.Execute();
        Assert.Equal(new Vector(6, 0), ship.GetProperty("Position"));
    }

    [Fact]
    public void Each_Game_Scope_Has_Its_Own_Operations_And_Receiver()
    {
        using var scope = _scope.Enter();
        var firstScope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        var secondScope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        var first = CreateGame("first", firstScope);
        var second = CreateGame("second", secondScope);
        var item = Item(fuel: 4);
        LongOperationCommands firstMovement;
        using (firstScope.Enter())
        {
            RegisterMovement(first);
            firstMovement = Movement(item);
        }
        using (secondScope.Enter())
        {
            RegisterMovement(second);
            var secondMovement = Movement(item);
            Assert.NotSame(firstMovement, secondMovement);
            Assert.NotSame(secondMovement, Movement(Item(fuel: 4)));
            second.Post(secondMovement.Start);
        }

        second.Execute();
        Assert.False(first.HasCommands);
        Assert.Single(second.Pending);
    }

    [Fact]
    public void Movement_Registration_Rejects_Invalid_Arguments()
    {
        using var scope = _scope.Enter();
        RegisterMovement(CreateGame("battle", _scope));

        Assert.Throws<ArgumentException>(() => Ioc.Resolve<LongOperationCommands>(MovementDependencies.CreateOperation));
        Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyLongMovement(null!));
    }

    private static Game CreateGame(string id, GameScope scope) =>
        new(id, new GameQueue(), scope, Mock.Of<IQuantum>(quantum => quantum.IsOver));

    private static void RegisterMovement(Game game, Action<IGameItem, Vector, Vector>? observe = null) =>
        new RegisterIoCDependencyLongMovement(new CommandReceiver(game.Post), observe).Execute();

    private static LongOperationCommands Movement(IGameItem item) =>
        Ioc.Resolve<LongOperationCommands>(MovementDependencies.CreateOperation, item);

    private static TestGameItem Item(int fuel)
    {
        var item = new TestGameItem();
        item.SetProperty("Position", new Vector(0, 0));
        item.SetProperty("Speed", 3);
        item.SetProperty("Direction", new Angle(0));
        item.SetProperty("Fuel", fuel);
        item.SetProperty("FuelConsumption", 2);
        return item;
    }
}
