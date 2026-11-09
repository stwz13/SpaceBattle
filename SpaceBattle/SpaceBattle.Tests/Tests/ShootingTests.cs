using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ShootingTests
{
    private readonly IGameItemStorage _storage = new GameItemStorage();

    public ShootingTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyExceptionFactory().Execute();
        new RegisterIoCDependencyGameItemFactory().Execute();
        new RegisterIoCDependencyComputedVelocity().Execute();
        new RegisterIoCDependencyProjectileFactory().Execute();
        new RegisterIoCDependencyGameItemStorage(_storage).Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(ShootCommand).Assembly, ShootingDependencies.All).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
    }

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static void InChildScope(Action action)
    {
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        try
        {
            action();
        }
        finally
        {
            SetScope(parent);
        }
    }

    private static IGameItem Ship(int ammo = 2) => new GameItemFactory().Create(new Dictionary<string, object>
    {
        ["Position"] = new Vector(12, 5),
        ["Direction"] = new Angle(0),
        ["ProjectileSpeed"] = 10,
        ["ProjectileDamage"] = 25,
        ["Ammo"] = ammo
    });

    private static Lib.ICommand Shooting(IGameItem ship) =>
        Ioc.Resolve<Lib.ICommand>("Commands.Action", "Shooting", ship);

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 7, 7)]
    [InlineData(2, 0, 10)]
    [InlineData(4, -10, 0)]
    [InlineData(6, 0, -10)]
    public void Configured_Action_Creates_A_Projectile_And_Consumes_One_Round(int direction, int vx, int vy)
    {
        var ship = Ship();
        ship.SetProperty("Direction", new Angle(direction));
        var action = Shooting(ship);
        Assert.Empty(_storage.GetSnapshot());
        Assert.Equal(2, ship.GetProperty("Ammo"));

        action.Execute();

        var projectile = Assert.Single(_storage.GetSnapshot());
        var id = Assert.IsType<string>(projectile.GetProperty("Id"));
        Assert.True(Guid.TryParseExact(id, "N", out _));
        Assert.Same(projectile, _storage.Get(id));
        Assert.Equal(new Vector(12, 5), projectile.GetProperty("Position"));
        Assert.Equal(new Angle(direction), projectile.GetProperty("Direction"));
        Assert.Equal(new Vector(vx, vy), projectile.GetProperty("Velocity"));
        Assert.Equal(10, projectile.GetProperty("Speed"));
        Assert.Equal(25, projectile.GetProperty("Damage"));
        Assert.Equal(false, projectile.GetProperty("Destroyed"));
        Assert.Equal(1, ship.GetProperty("Ammo"));
        Assert.Equal(new Vector(12, 5), ship.GetProperty("Position"));
        Assert.Throws<KeyNotFoundException>(() => ship.GetProperty("ProjectileFactory"));
        Assert.Throws<KeyNotFoundException>(() => ship.GetProperty("Storage"));
    }

    [Fact]
    public void Repeated_Execution_Uses_Current_Parameters_And_Creates_Distinct_Projectiles()
    {
        var ship = Ship();
        var action = Shooting(ship);
        action.Execute();
        var first = Assert.Single(_storage.GetSnapshot());
        ship.SetProperty("Position", new Vector(20, 30));
        ship.SetProperty("Direction", new Angle(2));
        ship.SetProperty("ProjectileSpeed", 4);
        ship.SetProperty("ProjectileDamage", 9);

        action.Execute();

        var second = Assert.Single(_storage.GetSnapshot(), item => !ReferenceEquals(item, first));
        Assert.NotEqual(first.GetProperty("Id"), second.GetProperty("Id"));
        Assert.Equal(new Vector(12, 5), first.GetProperty("Position"));
        Assert.Equal(new Vector(20, 30), second.GetProperty("Position"));
        Assert.Equal(new Vector(0, 4), second.GetProperty("Velocity"));
        Assert.Equal(9, second.GetProperty("Damage"));
        Assert.Equal(0, ship.GetProperty("Ammo"));
        Assert.Throws<InvalidOperationException>(action.Execute);
        Assert.Equal(2, _storage.GetSnapshot().Count);
    }

    [Fact]
    public void Position_Coordinates_Are_Copied_Independently_For_Each_Projectile()
    {
        var ship = Ship();
        var action = Shooting(ship);
        action.Execute();
        var first = Assert.Single(_storage.GetSnapshot());
        var position = Assert.IsType<Vector>(ship.GetProperty("Position"));
        position.Coordinates[0] = 99;
        action.Execute();
        var second = Assert.Single(_storage.GetSnapshot(), item => !ReferenceEquals(item, first));

        Assert.Equal(new Vector(12, 5), first.GetProperty("Position"));
        Assert.Equal(new Vector(99, 5), second.GetProperty("Position"));
        Assert.IsType<Vector>(second.GetProperty("Position")).Coordinates[1] = 88;
        Assert.Equal(new Vector(99, 5), position);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Empty_Ammo_Is_Rejected_Before_Resolving_Projectile_Dependencies(int ammo)
    {
        var ship = new GameItemFactory().Create(new Dictionary<string, object> { ["Ammo"] = ammo });

        Assert.Throws<InvalidOperationException>(Shooting(ship).Execute);

        Assert.Equal(ammo, ship.GetProperty("Ammo"));
        Assert.Empty(_storage.GetSnapshot());
    }

    [Theory]
    [InlineData("Position")]
    [InlineData("Direction")]
    [InlineData("ProjectileSpeed")]
    [InlineData("ProjectileDamage")]
    [InlineData("Ammo")]
    public void Missing_Properties_Do_Not_Create_A_Projectile_Or_Consume_Ammo(string missing)
    {
        var properties = new Dictionary<string, object>
        {
            ["Position"] = new Vector(1, 2), ["Direction"] = new Angle(0),
            ["ProjectileSpeed"] = 10, ["ProjectileDamage"] = 25, ["Ammo"] = 2
        };
        properties.Remove(missing);
        var ship = new GameItem(properties);

        Assert.Throws<KeyNotFoundException>(Shooting(ship).Execute);

        Assert.Empty(_storage.GetSnapshot());
        if (missing != "Ammo") Assert.Equal(2, ship.GetProperty("Ammo"));
    }

    [Theory]
    [InlineData("ProjectileSpeed")]
    [InlineData("ProjectileDamage")]
    public void Negative_Projectile_Parameters_Leave_Ammo_And_Storage_Unchanged(string property)
    {
        var ship = Ship();
        ship.SetProperty(property, -1);

        Assert.Throws<ArgumentOutOfRangeException>(Shooting(ship).Execute);

        Assert.Equal(2, ship.GetProperty("Ammo"));
        Assert.Empty(_storage.GetSnapshot());
    }

    [Fact]
    public void Creation_Failure_Does_Not_Consume_Ammo()
    {
        var ship = Ship();
        var factory = new Mock<IProjectileFactory>(MockBehavior.Strict);
        factory.Setup(value => value.Create(It.IsAny<Vector>(), It.IsAny<Angle>(), 10, 25))
            .Throws(new InvalidOperationException("Creation failed."));
        var action = Shooting(ship);
        InChildScope(() =>
        {
            Ioc.Resolve<App.ICommand>("IoC.Register", ShootingDependencies.Factory,
                (object[] args) => factory.Object).Execute();
            Assert.Throws<InvalidOperationException>(action.Execute);
        });

        Assert.Equal(2, ship.GetProperty("Ammo"));
        Assert.Empty(_storage.GetSnapshot());
        factory.VerifyAll();
    }

    [Fact]
    public void Storage_Failure_Does_Not_Consume_Ammo()
    {
        var ship = Ship();
        var storage = new Mock<IGameItemStorage>(MockBehavior.Strict);
        storage.Setup(value => value.Add(It.IsAny<string>(), It.IsAny<IGameItem>()))
            .Throws(new InvalidOperationException("Storage failed."));
        var action = Shooting(ship);
        InChildScope(() =>
        {
            new RegisterIoCDependencyGameItemStorage(storage.Object).Execute();
            Assert.Throws<InvalidOperationException>(action.Execute);
        });

        Assert.Equal(2, ship.GetProperty("Ammo"));
        Assert.Empty(_storage.GetSnapshot());
        storage.VerifyAll();
    }

    [Fact]
    public void Existing_Action_Adds_Projectiles_Only_To_The_Current_Battle()
    {
        var ship = Ship(3);
        var action = Shooting(ship);
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        var firstScope = Ioc.Resolve<object>("IoC.Scope.Create");
        var secondScope = Ioc.Resolve<object>("IoC.Scope.Create");
        var first = new BattleContextFactory().Create("First");
        var second = new BattleContextFactory().Create("Second");
        try
        {
            SetScope(firstScope);
            new RegisterIoCDependencyBattleContext(first).Execute();
            action.Execute();
            SetScope(secondScope);
            new RegisterIoCDependencyBattleContext(second).Execute();
            action.Execute();
            SetScope(firstScope);
            action.Execute();
        }
        finally
        {
            SetScope(parent);
        }

        Assert.Equal(2, first.Storage.GetSnapshot().Count);
        Assert.Single(second.Storage.GetSnapshot());
        Assert.Empty(_storage.GetSnapshot());
        Assert.Equal(0, ship.GetProperty("Ammo"));
    }

    [Fact]
    public void Shooting_Can_Be_A_Context_Action_For_A_Named_Shooter()
    {
        var ship = Ship();
        var other = Ship();
        var context = new CommandContext(new Dictionary<string, IGameItem>
        {
            ["Shooter"] = ship, ["Other"] = other
        });
        new RegisterIoCDependencyContextCommand("Context.Shoot",
            GeneratedNames.CommandDependency(typeof(ShootCommand)), "Shooter").Execute();
        new RegisterIoCDependencyContextActionComposition().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Register", "ContextShooting.Config",
            (object[] args) => new[] { "Context.Shoot" }).Execute();

        Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, "ContextShooting", context).Execute();

        Assert.Single(_storage.GetSnapshot());
        Assert.Equal(1, ship.GetProperty("Ammo"));
        Assert.Equal(2, other.GetProperty("Ammo"));
    }
}
