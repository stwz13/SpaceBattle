using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class AdditiveTransactionTests
{
    private const string Failure = "Tests.Transaction.Fail";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Reversible_And_Ordinary_Movement_Coexist_With_The_Same_Command_And_Objects()
    {
        await using var fixture = new RuntimeFixture(1);
        Configure(fixture);
        var game = fixture.Runtime.Start("match");
        await game.Execute(() =>
        {
            RegisterRules(("Atomic", [Key<BurnFuelCommand>(), Key<MoveCommand>()]));
            EnableTransactions();
            var ship = new GameItem(new Dictionary<string, object>
            {
                ["Fuel"] = 10, ["FuelConsumption"] = 3, ["Position"] = new Vector(1, 2)
            });
            var position = ship.GetProperty("Position");
            Assert.IsType<BattleContext>(Context());
            Assert.IsType<GameItemStorage>(Context().Storage);
            Assert.IsType<BurnFuelCommand>(Ioc.Resolve<Lib.ICommand>(Key<BurnFuelCommand>(), ship));

            Assert.Throws<KeyNotFoundException>(() => Action("Atomic", ship).Execute());
            Assert.Equal(10, ship.GetProperty("Fuel"));
            Assert.Same(position, ship.GetProperty("Position"));

            Ioc.Resolve<Lib.ICommand>(Key<BurnFuelCommand>(), ship).Execute();
            Assert.Equal(7, ship.GetProperty("Fuel"));
        }).WaitAsync(Timeout);
    }

    [Fact]
    public async Task A_Later_Step_Restores_The_Existing_Ship_And_Removes_Only_Its_New_Projectile()
    {
        await using var fixture = new RuntimeFixture(1);
        Configure(fixture);
        await fixture.Runtime.Start("match").Execute(() =>
        {
            RegisterRules(("Atomic", [Key<ShootCommand>(), Failure]), ("Shoot", [Key<ShootCommand>()]));
            EnableTransactions();
            var ship = new GameItem(new Dictionary<string, object>
            {
                ["Ammo"] = 2, ["Position"] = new Vector(0, 0), ["Direction"] = new Angle(0),
                ["ProjectileSpeed"] = 3, ["ProjectileDamage"] = 1
            });
            var storage = Context().Storage;
            storage.Add("ship", ship);

            Assert.Throws<InvalidOperationException>(() => Action("Atomic", ship).Execute());
            Assert.Same(ship, Assert.Single(storage.GetSnapshot()));
            Assert.Equal(2, ship.GetProperty("Ammo"));

            Action("Shoot", ship).Execute();
            Assert.Equal(1, ship.GetProperty("Ammo"));
            Assert.Equal(2, storage.GetSnapshot().Count);
        }).WaitAsync(Timeout);
    }

    [Fact]
    public async Task Contextual_Rollback_Restores_Both_Participants()
    {
        await using var fixture = new RuntimeFixture(1);
        Configure(fixture);
        await fixture.Runtime.Start("match").Execute(() =>
        {
            RegisterRules(("Atomic", ["Context.Refuel", Failure]), ("Refuel", ["Context.Refuel"]));
            EnableTransactions();
            var target = new GameItem(new Dictionary<string, object> { ["Fuel"] = 5 });
            var source = new GameItem(new Dictionary<string, object> { ["FuelReserve"] = 9, ["RefuelAmount"] = 3 });
            var context = new CommandContext(new Dictionary<string, IGameItem> { ["Target"] = target, ["Source"] = source });

            Assert.Throws<InvalidOperationException>(() =>
                Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, "Atomic", context).Execute());
            Assert.Equal(5, target.GetProperty("Fuel"));
            Assert.Equal(9, source.GetProperty("FuelReserve"));

            Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, "Refuel", context).Execute();
            Assert.Equal(8, target.GetProperty("Fuel"));
            Assert.Equal(6, source.GetProperty("FuelReserve"));
        }).WaitAsync(Timeout);
    }

    [Fact]
    public async Task A_Failed_Compensation_Stops_The_Game_And_Blocks_Another_Attempt()
    {
        await using var fixture = new RuntimeFixture(1);
        var attempts = 0;
        Configure(fixture);
        var effect = new TestEffect(Failure, (command, undo) => new CallbackCommand(() =>
        {
            attempts++;
            undo.Record(() => throw new IOException("Compensation failed."));
            command.Execute();
        }));

        var game = fixture.Runtime.Start("match");
        var other = fixture.Runtime.Start("other");
        await game.Execute(() =>
        {
            RegisterRules(("Atomic", [Failure]));
            EnableTransactions(effect);
            var action = Action("Atomic", new GameItem(new Dictionary<string, object>()));
            var error = Assert.Throws<TransactionRollbackException>(action.Execute);
            Assert.Collection(error.InnerExceptions,
                inner => Assert.IsType<InvalidOperationException>(inner), inner => Assert.IsType<IOException>(inner));
            Assert.Same(error, Assert.Throws<TransactionRollbackException>(action.Execute));
        }).WaitAsync(Timeout);

        Assert.Equal(1, attempts);
        Assert.Equal(GameHostState.Stopped, game.State);
        Assert.Equal(GameHostState.Running, other.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Failed_Nested_Rollback_Still_Restores_The_Outer_Steps(bool throwAfterCatching)
    {
        await using var fixture = new RuntimeFixture(1);
        const string nested = "Tests.Transaction.RunNested";
        var outsideError = new InvalidOperationException("Outer step failed.");
        Configure(fixture);
        var effect = new TestEffect(Failure, (command, undo) => new CallbackCommand(() =>
        {
            undo.Record(() => undo.Record(() => { }));
            command.Execute();
        }));
        using (fixture.Scope.Enter())
        {
            RuntimeFixture.Register(nested, args => new CallbackCommand(() =>
            {
                Assert.Throws<TransactionRollbackException>(() => Action("Inner", (IGameItem)args[0]).Execute());
                if (throwAfterCatching)
                    throw outsideError;
            }));
        }

        var game = fixture.Runtime.Start("match");
        await game.Execute(() =>
        {
            RegisterRules(("Outer", [Key<BurnFuelCommand>(), nested]), ("Inner", [Failure]));
            EnableTransactions(effect);
            var ship = new GameItem(new Dictionary<string, object> { ["Fuel"] = 10, ["FuelConsumption"] = 3 });
            var error = Assert.Throws<TransactionRollbackException>(() => Action("Outer", ship).Execute());
            Assert.Equal(10, ship.GetProperty("Fuel"));
            var errors = error.Flatten().InnerExceptions;
            Assert.Contains(errors, inner => inner.Message.Contains("cannot start another transaction", StringComparison.Ordinal));
            Assert.Equal(throwAfterCatching, errors.Contains(outsideError));
        }).WaitAsync(Timeout);
        Assert.Equal(GameHostState.Stopped, game.State);
    }

    [Fact]
    public async Task An_Outer_Failure_Restores_Writes_From_A_Successful_Nested_Macro()
    {
        const string nested = "Tests.Transaction.RunNested";
        await using var fixture = new RuntimeFixture(1);
        Configure(fixture);
        using (fixture.Scope.Enter())
        {
            RuntimeFixture.Register(nested, args => new CallbackCommand(() =>
            {
                var ship = (IGameItem)args[0];
                Action("Inner", ship).Execute();
                Assert.Equal(7, ship.GetProperty("Fuel"));
            }));
        }

        await fixture.Runtime.Start("match").Execute(() =>
        {
            RegisterRules(("Outer", [nested, Failure]), ("Inner", [Key<BurnFuelCommand>()]));
            EnableTransactions();
            var ship = new GameItem(new Dictionary<string, object> { ["Fuel"] = 10, ["FuelConsumption"] = 3 });
            var gameScope = Ioc.Resolve<object>("IoC.Scope.Current");

            var error = Assert.Throws<InvalidOperationException>(() => Action("Outer", ship).Execute());
            Assert.Equal("Step failed.", error.Message);
            Assert.Equal(10, ship.GetProperty("Fuel"));
            Assert.Same(gameScope, Ioc.Resolve<object>("IoC.Scope.Current"));
        }).WaitAsync(Timeout);
    }

    [Fact]
    public async Task A_Transactional_Hit_Uses_The_Existing_Damage_And_Destruction_Commands()
    {
        await using var fixture = new RuntimeFixture(1);
        Configure(fixture);
        await fixture.Runtime.Start("match").Execute(() =>
        {
            RegisterRules(("Hit", ["Context.ApplyDamage", "Context.DestroyIfDead"]));
            EnableTransactions();
            var ship = new GameItem(new Dictionary<string, object> { ["Health"] = 3, ["Destroyed"] = false });
            var projectile = new GameItem(new Dictionary<string, object> { ["Damage"] = 4 });
            var context = new CommandContext(new Dictionary<string, IGameItem> { ["Target"] = ship, ["Source"] = projectile });

            Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, "Hit", context).Execute();
            Assert.Equal(0, ship.GetProperty("Health"));
            Assert.Equal(true, ship.GetProperty("Destroyed"));
        }).WaitAsync(Timeout);
    }

    private static void Configure(RuntimeFixture fixture)
    {
        using var scope = fixture.Scope.Enter();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyComputedVelocity().Execute();
        new RegisterIoCDependencyProjectileFactory().Execute();
        new RegisterIoCDependencyGeneratedStrategiesWithAdapters([typeof(MoveCommand).Assembly],
            [typeof(IRefuelTarget), typeof(IRefuelSource), typeof(IDamageTarget), typeof(IDamageSource)],
            ShootingDependencies.All).Execute();
        new RegisterIoCDependencyRefuelContextCommand("Context.Refuel", "Target", "Source").Execute();
        new RegisterIoCDependencyDamageContextCommand("Context.ApplyDamage", "Target", "Source").Execute();
        new RegisterIoCDependencyContextCommand("Context.DestroyIfDead", Key<DestroyIfDeadCommand>(), "Target").Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyContextActionComposition().Execute();
        RuntimeFixture.Register(Failure, _ => new CallbackCommand(() => throw new InvalidOperationException("Step failed.")));
    }

    [Fact]
    public async Task A_New_Generated_Command_In_An_Ordinary_Config_Rolls_Back_Automatically()
    {
        await using var fixture = new RuntimeFixture(1);
        Configure(fixture);
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var calibrate = Key<CalibrateCommand>();
            File.WriteAllLines(Path.Combine(directory.FullName, "Calibration.config"), [calibrate, Failure]);
            using (fixture.Scope.Enter())
            {
                new RegisterIoCDependencyGeneratedStrategies(typeof(CalibrateCommand).Assembly, []).Execute();
                new RegisterIoCDependencyActionConfigurations(directory.FullName).Execute();
            }

            await fixture.Runtime.Start("match").Execute(() =>
            {
                EnableTransactions();
                var item = new GameItem(new Dictionary<string, object> { ["Level"] = 8, ["Set"] = 10 });

                var error = Assert.Throws<InvalidOperationException>(() => Action("Calibration", item).Execute());
                Assert.Equal("Step failed.", error.Message);
                Assert.Equal(8, item.GetProperty("Level"));

                Ioc.Resolve<Lib.ICommand>(calibrate, item).Execute();
                Assert.Equal(20, item.GetProperty("Level"));
                Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Unused"));
            }).WaitAsync(Timeout);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Shooting_Removes_A_Projectile_When_The_Ammo_Write_Throws()
    {
        await using var fixture = new RuntimeFixture(1);
        Configure(fixture);
        await fixture.Runtime.Start("match").Execute(() =>
        {
            RegisterRules(("Shoot", [Key<ShootCommand>()]));
            EnableTransactions();
            var ship = new FailingAmmoItem(new GameItem(new Dictionary<string, object>
            {
                ["Ammo"] = 2, ["Position"] = new Vector(0, 0), ["Direction"] = new Angle(0),
                ["ProjectileSpeed"] = 3, ["ProjectileDamage"] = 1
            }));
            var storage = Context().Storage;
            storage.Add("ship", ship);
            var oldProjectile = new GameItem(new Dictionary<string, object> { ["Id"] = "old-projectile" });
            storage.Add("old-projectile", oldProjectile);

            Assert.Throws<IOException>(() => Action("Shoot", ship).Execute());
            Assert.Equal(2, ship.GetProperty("Ammo"));
            Assert.Equal(2, storage.GetSnapshot().Count);
            Assert.Same(ship, storage.Get("ship"));
            Assert.Same(oldProjectile, storage.Get("old-projectile"));
        }).WaitAsync(Timeout);
    }

    public interface ICalibration
    {
        int Set { get; }
        int Level { set; }
        int Unused { set; }
    }

    public sealed class CalibrateCommand(ICalibration calibration) : Lib.ICommand
    {
        public void Execute()
        {
            calibration.Level = calibration.Set;
            calibration.Level = 20;
        }
    }

    private sealed class FailingAmmoItem(IGameItem item) : IGameItem
    {
        public object GetProperty(string name) => item.GetProperty(name);

        public void SetProperty(string name, object value)
        {
            item.SetProperty(name, value);
            if (name == "Ammo" && value is 1)
                throw new IOException("Ammo write failed after updating the object.");
        }
    }

    private static IBattleContext Context() => Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context);

    private static Lib.ICommand Action(string name, IGameItem item) =>
        Ioc.Resolve<Lib.ICommand>("Commands.Action", name, item);

    private static string Key<T>() => GeneratedNames.CommandDependency(typeof(T));

    private static void RegisterRules(params (string Action, string[] Steps)[] rules)
    {
        foreach (var step in rules.SelectMany(rule => rule.Steps).Distinct(StringComparer.Ordinal))
            RegisterIoCDependencyRuleCommand.FromExisting(step).Execute();

        new RegisterIoCDependencyGameActionRules(new GameActionRules(
            rules.ToDictionary(rule => rule.Action, rule => rule.Steps))).Execute();
    }

    private static void EnableTransactions(params ITransactionCommandEffect[] effects) =>
        new RegisterIoCDependencyActionTransactions(effects.Prepend(new ProjectileInsertionEffect())).Execute();

    private sealed class TestEffect(string key, Func<Lib.ICommand, ITransactionUndo, Lib.ICommand> decorate)
        : ITransactionCommandEffect
    {
        public string CommandKey => key;
        public Lib.ICommand Decorate(object[] args, Lib.ICommand command, ITransactionUndo undo) =>
            decorate(command, undo);
    }
}
