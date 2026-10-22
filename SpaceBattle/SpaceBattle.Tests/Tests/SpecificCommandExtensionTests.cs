using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using SpaceBattle.Tests.VelocityExtensions;

namespace SpaceBattle.Tests;

public class SpecificCommandExtensionTests
{
    public SpecificCommandExtensionTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategiesWithAdapters([typeof(CapturePairVelocityCommand).Assembly],
            [typeof(IVelocitySnapshot)],
            [new Dependency(typeof(IVelocitySnapshot), nameof(IVelocitySnapshot.Velocity),
                ComputedVelocityDependencies.Velocity)]).Execute();
        new RegisterIoCDependencyComputedVelocity().Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyContextActionComposition().Execute();
        Register("Context.CapturePair", args =>
        {
            var context = (ICommandContext)args[0];
            return new CapturePairVelocityCommand(Adapt(context.GetItem("First")), Adapt(context.GetItem("Second")));
        });
        Register("CapturePair.Config", _ => new[] { "Context.CapturePair" });
    }

    private static IVelocitySnapshot Adapt(IGameItem item) =>
        Ioc.Resolve<IVelocitySnapshot>(GeneratedNames.AdapterDependency(typeof(IVelocitySnapshot)), item);

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static TestGameItem Item(int speed, int direction)
    {
        var item = new TestGameItem();
        item.SetProperty("Speed", speed);
        item.SetProperty("Direction", new Angle(direction));
        return item;
    }

    private static ICommandContext Context(IGameItem first, IGameItem second) => new CommandContext(
        new Dictionary<string, IGameItem> { ["First"] = first, ["Second"] = second });

    private static Lib.ICommand Action(ICommandContext context) =>
        Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, "CapturePair", context);

    [Fact]
    public void Two_Participants_Of_The_Same_Interface_Use_Their_Own_Computed_Properties()
    {
        var first = Item(10, 1);
        var second = Item(4, 2);
        var action = Action(Context(first, second));

        Assert.Throws<KeyNotFoundException>(() => first.GetProperty("VelocitySnapshot"));
        Assert.Throws<KeyNotFoundException>(() => second.GetProperty("VelocitySnapshot"));
        action.Execute();

        Assert.Equal(new Vector(7, 7), first.GetProperty("VelocitySnapshot"));
        Assert.Equal(new Vector(0, 4), second.GetProperty("VelocitySnapshot"));
        Assert.Throws<KeyNotFoundException>(() => first.GetProperty("Velocity"));
        Assert.Throws<KeyNotFoundException>(() => second.GetProperty("Velocity"));

        first.SetProperty("Speed", 2);
        second.SetProperty("Direction", new Angle(4));
        action.Execute();

        Assert.Equal(new Vector(1, 1), first.GetProperty("VelocitySnapshot"));
        Assert.Equal(new Vector(-4, 0), second.GetProperty("VelocitySnapshot"));
    }

    [Fact]
    public void An_Existing_Specific_Command_Uses_Child_Property_Overrides_And_Restores_Parent_Behavior()
    {
        var first = Item(10, 0);
        var second = Item(4, 2);
        var action = Action(Context(first, second));
        var received = new List<object>();
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        try
        {
            Register(ComputedVelocityDependencies.Velocity, args =>
            {
                received.Add(args[0]);
                return new Vector(8, 9);
            });

            action.Execute();

            Assert.Equal(new object[] { first, second }, received);
            Assert.Equal(new Vector(8, 9), first.GetProperty("VelocitySnapshot"));
            Assert.Equal(new Vector(8, 9), second.GetProperty("VelocitySnapshot"));
        }
        finally
        {
            SetScope(parent);
        }

        action.Execute();

        Assert.Equal(new Vector(10, 0), first.GetProperty("VelocitySnapshot"));
        Assert.Equal(new Vector(0, 4), second.GetProperty("VelocitySnapshot"));
    }

    [Fact]
    public void Runtime_Composition_And_Execution_Do_Not_Require_Generation_Services()
    {
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        try
        {
            var finder = new Mock<ICommandFinder>(MockBehavior.Strict);
            var compiler = new Mock<ISourceCompiler>(MockBehavior.Strict);
            var adapters = new Mock<IAdapterSourceBuilder>(MockBehavior.Strict);
            var strategies = new Mock<IStrategySourceBuilder>(MockBehavior.Strict);
            Register("CodeGeneration.CommandFinder", _ => finder.Object);
            Register("CodeGeneration.SourceCompiler", _ => compiler.Object);
            Register("CodeGeneration.AdapterSourceBuilder", _ => adapters.Object);
            Register("CodeGeneration.StrategySourceBuilder", _ => strategies.Object);
            var first = Item(10, 0);
            var second = Item(4, 2);

            Action(Context(first, second)).Execute();

            Assert.Equal(new Vector(10, 0), first.GetProperty("VelocitySnapshot"));
            Assert.Equal(new Vector(0, 4), second.GetProperty("VelocitySnapshot"));
            finder.VerifyNoOtherCalls();
            compiler.VerifyNoOtherCalls();
            adapters.VerifyNoOtherCalls();
            strategies.VerifyNoOtherCalls();
        }
        finally
        {
            SetScope(parent);
        }
    }
}
