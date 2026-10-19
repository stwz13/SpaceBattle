using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using SpaceBattle.Tests.Extensions;
using SpaceBattle.Tests.FactoryExtensions;
using SpaceBattle.Tests.VelocityExtensions;

namespace SpaceBattle.Tests;

public class GameItemFactoryRegistrationTests
{
    public GameItemFactoryRegistrationTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyGameItemFactory().Execute();
    }

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static IGameItemFactory Factory() =>
        Ioc.Resolve<IGameItemFactory>(GameItemFactoryDependencies.Factory);

    private static void Generate(params Dependency[] dependencies)
    {
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(CreateGameItemCommand).Assembly,
            dependencies.Prepend(new Dependency(typeof(IGameItemCreation), nameof(IGameItemCreation.Factory),
                GameItemFactoryDependencies.Factory))).Execute();
    }

    [Fact]
    public void Registration_Provides_A_Factory_For_Empty_And_Initialized_Items()
    {
        var empty = Factory().Create();
        var initialized = Factory().Create(new Dictionary<string, object> { ["Value"] = 5 });

        empty.SetProperty("Value", 3);

        Assert.Equal(3, empty.GetProperty("Value"));
        Assert.Equal(5, initialized.GetProperty("Value"));
    }

    [Fact]
    public void Factory_Is_Injected_Through_A_Generated_Adapter_Into_A_Configured_Action()
    {
        Generate();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Register", "CreateItem.Config",
            (object[] args) => new[] { GeneratedNames.CommandDependency(typeof(CreateGameItemCommand)) }).Execute();
        var properties = new Dictionary<string, object> { ["CustomValue"] = 42 };
        var context = Factory().Create(new Dictionary<string, object> { ["InitialProperties"] = properties });

        var action = Ioc.Resolve<Lib.ICommand>("Commands.Action", "CreateItem", context);
        Assert.Throws<KeyNotFoundException>(() => context.GetProperty("CreatedItem"));
        action.Execute();

        var created = Assert.IsAssignableFrom<IGameItem>(context.GetProperty("CreatedItem"));
        Assert.Equal(42, created.GetProperty("CustomValue"));
        action.Execute();
        Assert.NotSame(created, context.GetProperty("CreatedItem"));
        Assert.Throws<KeyNotFoundException>(() => context.GetProperty("Factory"));
    }

    [Fact]
    public void Created_Items_Work_With_Existing_Generated_Commands()
    {
        Generate();
        var item = Factory().Create(new Dictionary<string, object> { ["Counter"] = 1, ["Step"] = 2 });

        Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(IncrementCommand)), item).Execute();

        Assert.Equal(3, item.GetProperty("Counter"));
    }

    [Fact]
    public void Created_Items_Support_Computed_Properties_Without_Storing_Them()
    {
        new RegisterIoCDependencyComputedVelocity().Execute();
        Generate(new Dependency(typeof(IVelocitySnapshot), nameof(IVelocitySnapshot.Velocity),
            ComputedVelocityDependencies.Velocity));
        var item = Factory().Create(new Dictionary<string, object> { ["Speed"] = 10, ["Direction"] = new Angle(1) });

        Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(CaptureVelocityCommand)), item).Execute();

        Assert.Equal(new Vector(7, 7), item.GetProperty("VelocitySnapshot"));
        Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Velocity"));
    }

    [Fact]
    public void Child_Scope_Can_Replace_The_Factory_For_An_Existing_Command()
    {
        Generate();
        var properties = new Dictionary<string, object> { ["Value"] = 1 };
        var context = Factory().Create(new Dictionary<string, object> { ["InitialProperties"] = properties });
        var command = Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(CreateGameItemCommand)), context);
        var replacement = new Mock<IGameItemFactory>(MockBehavior.Strict);
        var replacementItem = new Mock<IGameItem>().Object;
        replacement.Setup(factory => factory.Create(properties)).Returns(replacementItem);
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        try
        {
            Ioc.Resolve<App.ICommand>("IoC.Register", GameItemFactoryDependencies.Factory,
                (object[] args) => replacement.Object).Execute();

            command.Execute();

            Assert.Same(replacementItem, context.GetProperty("CreatedItem"));
            replacement.Verify(factory => factory.Create(properties), Times.Once);
            replacement.VerifyNoOtherCalls();
        }
        finally
        {
            SetScope(parent);
        }

        command.Execute();
        var created = Assert.IsAssignableFrom<IGameItem>(context.GetProperty("CreatedItem"));
        Assert.NotSame(replacementItem, created);
        Assert.Equal(1, created.GetProperty("Value"));
    }
}
