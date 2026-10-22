using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using SpaceBattle.Tests.ContextExtensions;
using SpaceBattle.Tests.Extensions;
using SpaceBattle.Tests.VelocityExtensions;

namespace SpaceBattle.Tests;

public class ExplicitAdapterRegistrationTests
{
    public ExplicitAdapterRegistrationTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
    }

    private static T Adapt<T>(IGameItem item) =>
        Ioc.Resolve<T>(GeneratedNames.AdapterDependency(typeof(T)), item);

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    [Fact]
    public void Interfaces_Of_A_Multiple_Parameter_Command_Can_Be_Prepared_Without_Finder()
    {
        Assert.DoesNotContain(new CommandFinder().Find(typeof(TransferValueCommand).Assembly),
            command => command.Command == typeof(TransferValueCommand));
        var child = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", child).Execute();
        var finder = new Mock<ICommandFinder>(MockBehavior.Strict);
        Register("CodeGeneration.CommandFinder", _ => finder.Object);

        new RegisterIoCDependencyGeneratedAdapters([typeof(ITransferTarget), typeof(ITransferSource)], []).Execute();
        var target = new TestGameItem();
        target.SetProperty("Amount", 3);
        var source = new TestGameItem();
        source.SetProperty("Amount", 5);
        new TransferValueCommand(Adapt<ITransferTarget>(target), Adapt<ITransferSource>(source)).Execute();

        Assert.Equal(8, target.GetProperty("Amount"));
        Assert.Equal(0, source.GetProperty("Amount"));
        finder.VerifyNoOtherCalls();
    }

    [Fact]
    public void Repeated_Interfaces_And_Identical_Descriptions_Are_Registered_Once()
    {
        var dependency = new Dependency(typeof(ITransferTarget), "Amount", "Test.Amount");
        new RegisterIoCDependencyGeneratedAdapters([typeof(ITransferTarget), typeof(ITransferTarget)],
            [dependency, dependency]).Execute();
        Register("Test.Amount", _ => 9);

        Assert.Equal(9, Adapt<ITransferTarget>(new TestGameItem()).Amount);
    }

    [Fact]
    public void Combined_Preparation_Deduplicates_Standard_And_Explicit_Interfaces_And_Assemblies()
    {
        var assembly = typeof(IncrementCommand).Assembly;
        new RegisterIoCDependencyGeneratedStrategiesWithAdapters([assembly, assembly],
            [typeof(ICounterView), typeof(ICounterView), typeof(ITransferTarget)], []).Execute();
        var item = new TestGameItem();
        item.SetProperty("Counter", 1);
        item.SetProperty("Step", 2);
        item.SetProperty("Amount", 7);

        Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(IncrementCommand)), item).Execute();

        Assert.Equal(3, item.GetProperty("Counter"));
        Assert.Equal(7, Adapt<ITransferTarget>(item).Amount);
    }

    [Fact]
    public void Additional_Explicit_Interfaces_Can_Be_Prepared_After_Legacy_Standard_Registration()
    {
        new RegisterIoCDependencyGeneratedStrategies(typeof(IncrementCommand).Assembly, []).Execute();
        new RegisterIoCDependencyGeneratedAdapters([typeof(ITransferSource)], []).Execute();
        var item = new TestGameItem();
        item.SetProperty("Amount", 4);

        Assert.Equal(4, Adapt<ITransferSource>(item).Amount);
    }

    [Fact]
    public void Explicit_Adapters_Preserve_Inherited_And_Hidden_Property_Bindings()
    {
        new RegisterIoCDependencyGeneratedAdapters([typeof(IBaseView), typeof(IDerivedView), typeof(IHiddenView)],
            [new Dependency(typeof(IBaseView), "InheritedValue", "Test.Base"),
             new Dependency(typeof(IDerivedView), "InheritedValue", "Test.Derived"),
             new Dependency(typeof(IHiddenBase), "Value", "Test.Int"),
             new Dependency(typeof(IHiddenView), "Value", "Test.Text")]).Execute();
        Register("Test.Base", _ => 11);
        Register("Test.Derived", _ => 22);
        Register("Test.Int", _ => 33);
        Register("Test.Text", _ => "text");
        var item = new TestGameItem();

        Assert.Equal(11, Adapt<IBaseView>(item).InheritedValue);
        var derived = Adapt<IDerivedView>(item);
        Assert.Equal(22, derived.InheritedValue);
        derived.BaseResult = 44;
        Assert.Equal(44, item.GetProperty("BaseResult"));
        var hidden = Adapt<IHiddenView>(item);
        Assert.Equal("text", hidden.Value);
        Assert.Equal(33, ((IHiddenBase)hidden).Value);
    }

    [Fact]
    public void Explicit_Adapter_Uses_Current_Computed_Properties_Without_Storing_Them()
    {
        new RegisterIoCDependencyComputedVelocity().Execute();
        new RegisterIoCDependencyGeneratedAdapters([typeof(IVelocitySnapshot)],
            [new Dependency(typeof(IVelocitySnapshot), "Velocity", ComputedVelocityDependencies.Velocity)]).Execute();
        var item = new TestGameItem();
        item.SetProperty("Speed", 10);
        item.SetProperty("Direction", new Angle(1));
        var view = Adapt<IVelocitySnapshot>(item);

        Assert.Equal(new Vector(7, 7), view.Velocity);
        item.SetProperty("Direction", new Angle(2));
        Assert.Equal(new Vector(0, 10), view.Velocity);
        Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Velocity"));
    }

    [Fact]
    public void Explicit_Adapters_Support_Nested_Generic_Property_Types()
    {
        new RegisterIoCDependencyGeneratedAdapters([typeof(INestedView)], []).Execute();
        var value = new Outer<int>.Inner<string>();
        var item = new TestGameItem();
        item.SetProperty("Nested", value);

        Assert.Same(value, Adapt<INestedView>(item).Nested);
    }

    [Fact]
    public void Ambiguous_Descriptions_Fail_Before_Registering_Adapters()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new RegisterIoCDependencyGeneratedAdapters([typeof(ITransferTarget)],
                [new Dependency(typeof(ITransferTarget), "Amount", "Test.First"),
                 new Dependency(typeof(ITransferTarget), "Amount", "Test.Second")]).Execute());

        new RegisterIoCDependencyGeneratedAdapters([typeof(ITransferTarget)], []).Execute();
        var item = new TestGameItem();
        item.SetProperty("Amount", 6);
        Assert.Equal(6, Adapt<ITransferTarget>(item).Amount);
    }

    [Theory]
    [InlineData(typeof(Vector))]
    [InlineData(typeof(Lib.ICommand))]
    [InlineData(typeof(IList<>))]
    public void Unsupported_Interfaces_Fail_Before_Registering_Valid_Interfaces(Type unsupported)
    {
        Assert.Throws<ArgumentException>(() =>
            new RegisterIoCDependencyGeneratedAdapters([typeof(ITransferTarget), unsupported], []).Execute());

        new RegisterIoCDependencyGeneratedAdapters([typeof(ITransferTarget)], []).Execute();
    }

    [Fact]
    public void Empty_Explicit_Preparation_Does_Not_Require_Generation_Dependencies()
    {
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        Register("CodeGeneration.AdapterSourceBuilder", _ => throw new InvalidOperationException());
        Register("CodeGeneration.StrategySourceBuilder", _ => throw new InvalidOperationException());
        Register("CodeGeneration.SourceCompiler", _ => throw new InvalidOperationException());

        new RegisterIoCDependencyGeneratedAdapters([], []).Execute();
    }
}
