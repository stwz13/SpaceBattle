using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using SpaceBattle.Tests.ContextExtensions;
using SpaceBattle.Tests.Extensions;

namespace SpaceBattle.Tests;

public class ContextActionCompositionTests
{
    public ContextActionCompositionTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategiesWithAdapters([typeof(TransferValueCommand).Assembly],
            [typeof(ITransferTarget), typeof(ITransferSource)], []).Execute();
        new RegisterIoCDependencyExceptionFactory().Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyContextActionComposition().Execute();
        new RegisterIoCDependencyContextCommand("Context.Increment",
            GeneratedNames.CommandDependency(typeof(IncrementCommand)), "Target").Execute();
        new RegisterIoCDependencyContextCommand("Context.Record",
            GeneratedNames.CommandDependency(typeof(RecordCounterCommand)), "Target").Execute();
        Register("Context.Check", args =>
        {
            var context = (ICommandContext)args[0];
            return new CheckTransferSourceCommand(Adapt<ITransferSource>(context.GetItem("Source")),
                Ioc.Resolve<IExceptionFactory>("Factories.Exception"));
        });
        Register("Context.Transfer", args =>
        {
            var context = (ICommandContext)args[0];
            return new TransferValueCommand(Adapt<ITransferTarget>(context.GetItem("Target")),
                Adapt<ITransferSource>(context.GetItem("Source")));
        });
    }

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static T Adapt<T>(IGameItem item) =>
        Ioc.Resolve<T>(GeneratedNames.AdapterDependency(typeof(T)), item);

    private static TestGameItem Item(int amount, int counter = 1, int step = 2)
    {
        var item = new TestGameItem();
        item.SetProperty("Amount", amount);
        item.SetProperty("Counter", counter);
        item.SetProperty("Step", step);
        return item;
    }

    private static ICommandContext Context(IGameItem target, IGameItem source) =>
        new CommandContext(new Dictionary<string, IGameItem> { ["Target"] = target, ["Source"] = source });

    private static Lib.ICommand Action(string name, ICommandContext context) =>
        Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, name, context);

    private static void Config(string name, params string[] commands) => Register(name + ".Config", _ => commands);

    [Fact]
    public void File_Configuration_Composes_Specific_And_Standard_Commands_And_Can_Be_Extended()
    {
        var directory = Directory.CreateTempSubdirectory("SpaceBattle-Context-");
        try
        {
            var path = Path.Combine(directory.FullName, "Transfer.config");
            File.WriteAllLines(path, ["Context.Check", "Context.Transfer", "Context.Increment"]);
            new RegisterIoCDependencyActionConfigurations(directory.FullName).Execute();
            var target = Item(3);
            var source = Item(5, 100, 20);
            var context = Context(target, source);

            var action = Action("Transfer", context);
            Assert.Equal(3, target.GetProperty("Amount"));
            Assert.Equal(5, source.GetProperty("Amount"));
            action.Execute();

            Assert.Equal(8, target.GetProperty("Amount"));
            Assert.Equal(0, source.GetProperty("Amount"));
            Assert.Equal(3, target.GetProperty("Counter"));
            Assert.Equal(100, source.GetProperty("Counter"));

            File.AppendAllText(path, "Context.Record\n");
            source.SetProperty("Amount", 2);
            Action("Transfer", context).Execute();

            Assert.Equal(10, target.GetProperty("Amount"));
            Assert.Equal(5, target.GetProperty("Counter"));
            Assert.Equal(5, target.GetProperty("RecordedCounter"));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void The_Same_Context_Instance_Is_Passed_To_Every_Command_In_Configuration_Order()
    {
        var context = Context(Item(1), Item(2));
        var received = new List<object>();
        var executed = new List<string>();
        foreach (var key in new[] { "Context.First", "Context.Second" })
        {
            Register(key, args =>
            {
                received.Add(args[0]);
                var command = new Mock<Lib.ICommand>();
                command.Setup(value => value.Execute()).Callback(() => executed.Add(key));
                return command.Object;
            });
        }
        Config("Ordered", "Context.Second", "Context.First");

        var action = Action("Ordered", context);

        Assert.Equal(2, received.Count);
        Assert.All(received, value => Assert.Same(context, value));
        Assert.Empty(executed);
        action.Execute();
        Assert.Equal(new[] { "Context.Second", "Context.First" }, executed);
    }

    [Fact]
    public void Failed_Check_Prevents_Subsequent_Commands_From_Executing()
    {
        Config("Transfer", "Context.Check", "Context.Transfer", "Context.Increment");
        var target = Item(3);
        var source = Item(0);
        var action = Action("Transfer", Context(target, source));

        Assert.Throws<InvalidOperationException>(() => action.Execute());

        Assert.Equal(3, target.GetProperty("Amount"));
        Assert.Equal(0, source.GetProperty("Amount"));
        Assert.Equal(1, target.GetProperty("Counter"));
    }

    [Fact]
    public void Missing_Role_Fails_During_Composition_Without_Executing_Prepared_Commands()
    {
        Config("Transfer", "Context.Increment", "Context.Transfer");
        var target = Item(3);
        var context = new CommandContext(new Dictionary<string, IGameItem> { ["Target"] = target });

        Assert.Throws<KeyNotFoundException>(() => Action("Transfer", context));

        Assert.Equal(1, target.GetProperty("Counter"));
        Assert.Equal(3, target.GetProperty("Amount"));
    }

    [Fact]
    public void Missing_Command_Fails_During_Composition_Without_Executing_Prepared_Commands()
    {
        Config("Missing", "Context.Increment", "Context.DoesNotExist");
        var target = Item(3);

        Assert.ThrowsAny<Exception>(() => Action("Missing", Context(target, Item(5))));

        Assert.Equal(1, target.GetProperty("Counter"));
    }

    [Fact]
    public void An_Existing_Action_Reads_Current_Properties_On_Every_Execution()
    {
        Config("Transfer", "Context.Check", "Context.Transfer");
        var target = Item(3);
        var source = Item(5);
        var action = Action("Transfer", Context(target, source));
        source.SetProperty("Amount", 7);

        action.Execute();
        Assert.Equal(10, target.GetProperty("Amount"));
        source.SetProperty("Amount", 2);
        action.Execute();
        Assert.Equal(12, target.GetProperty("Amount"));
        Assert.Throws<InvalidOperationException>(() => action.Execute());
    }

    [Fact]
    public void Contexts_Isolate_The_Participants_Of_Independent_Operations()
    {
        Config("Transfer", "Context.Transfer");
        var firstTarget = Item(1);
        var firstSource = Item(2);
        var secondTarget = Item(10);
        var secondSource = Item(20);

        Action("Transfer", Context(secondTarget, secondSource)).Execute();

        Assert.Equal(30, secondTarget.GetProperty("Amount"));
        Assert.Equal(0, secondSource.GetProperty("Amount"));
        Assert.Equal(1, firstTarget.GetProperty("Amount"));
        Assert.Equal(2, firstSource.GetProperty("Amount"));
    }

    [Fact]
    public void Contexts_Support_Additional_Roles_Without_Changing_The_Infrastructure()
    {
        new RegisterIoCDependencyContextCommand("Context.ThirdIncrement",
            GeneratedNames.CommandDependency(typeof(IncrementCommand)), "Third").Execute();
        Config("ThreeItems", "Context.Transfer", "Context.ThirdIncrement");
        var target = Item(3);
        var source = Item(5);
        var third = Item(100, 10, 4);
        var context = new CommandContext(new Dictionary<string, IGameItem>
            { ["Target"] = target, ["Source"] = source, ["Third"] = third });

        Action("ThreeItems", context).Execute();

        Assert.Equal(8, target.GetProperty("Amount"));
        Assert.Equal(14, third.GetProperty("Counter"));
    }

    [Fact]
    public void Legacy_Single_Item_Actions_Work_Alongside_Context_Actions()
    {
        Config("Counter", GeneratedNames.CommandDependency(typeof(IncrementCommand)));
        Config("Transfer", "Context.Transfer");
        var target = Item(3);

        Ioc.Resolve<Lib.ICommand>("Commands.Action", "Counter", target).Execute();
        Action("Transfer", Context(target, Item(5))).Execute();

        Assert.Equal(3, target.GetProperty("Counter"));
        Assert.Equal(8, target.GetProperty("Amount"));
    }

    [Fact]
    public void Child_Scope_Can_Replace_A_Specific_Command_Without_Changing_The_Parent()
    {
        Config("Transfer", "Context.Transfer");
        var target = Item(3);
        var source = Item(5);
        var context = Context(target, source);
        var replacement = new Mock<Lib.ICommand>();
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        try
        {
            Register("Context.Transfer", args =>
            {
                Assert.Same(context, args[0]);
                return replacement.Object;
            });

            Action("Transfer", context).Execute();

            replacement.Verify(command => command.Execute(), Times.Once);
            Assert.Equal(3, target.GetProperty("Amount"));
        }
        finally
        {
            SetScope(parent);
        }

        Action("Transfer", context).Execute();
        Assert.Equal(8, target.GetProperty("Amount"));
    }

    [Fact]
    public void Empty_Configuration_Produces_An_Executable_Empty_Macro()
    {
        Config("Empty");
        var target = Item(3);

        Action("Empty", Context(target, Item(5))).Execute();

        Assert.Equal(3, target.GetProperty("Amount"));
    }

    [Fact]
    public void Missing_Configuration_Fails_During_Composition()
    {
        Assert.ThrowsAny<Exception>(() => Action("Missing", Context(Item(3), Item(5))));
    }

    [Fact]
    public void Incorrect_Context_Action_Arguments_Are_Rejected()
    {
        var context = Context(Item(3), Item(5));
        object[][] invalidArguments = [[], ["Action"], ["", context], [" ", context],
            ["Action", new object()], ["Action", context, new object()]];

        foreach (var args in invalidArguments)
        {
            Assert.ThrowsAny<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, args));
        }
    }

    [Fact]
    public void Context_Bridge_Requires_A_Context_And_A_Configured_Role()
    {
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>("Context.Increment", Item(3)));
        Assert.Throws<KeyNotFoundException>(() => Ioc.Resolve<Lib.ICommand>("Context.Increment",
            new CommandContext(new Dictionary<string, IGameItem> { ["Source"] = Item(5) })));
        Assert.Throws<ArgumentException>(() => new RegisterIoCDependencyContextCommand("Same", "Same", "Target"));
    }
}
