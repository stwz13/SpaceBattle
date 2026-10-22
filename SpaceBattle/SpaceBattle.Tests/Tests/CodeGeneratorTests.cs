using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using SpaceBattle.Tests.Extensions;

namespace SpaceBattle.Tests;

public class CodeGeneratorTests
{
    public CodeGeneratorTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(IncrementCommand).Assembly,
            [new Dependency(typeof(ICounterView), "Step", "Test.Step")]).Execute();
    }

    [Fact]
    public void CommandFinder_Finds_Commands_With_Single_Property_Interface_Parameter()
    {
        var commands = new CommandFinder().Find(typeof(IncrementCommand).Assembly);

        Assert.Contains((typeof(IncrementCommand), typeof(ICounterView)), commands);
        Assert.Contains((typeof(RecordCounterCommand), typeof(IRecordedCounterView)), commands);
        var infrastructure = new CommandFinder().Find(typeof(MacroCommand).Assembly);
        Assert.DoesNotContain(infrastructure, entry => entry.Command == typeof(MacroCommand));
        Assert.DoesNotContain(infrastructure, entry => entry.Command == typeof(SetPropertyCommand));
    }

    [Fact]
    public void IsAdaptable_Rejects_Interfaces_With_Methods_And_Classes()
    {
        Assert.True(InterfaceInspector.IsAdaptable(typeof(ICounterView)));
        Assert.False(InterfaceInspector.IsAdaptable(typeof(Lib.ICommand)));
        Assert.False(InterfaceInspector.IsAdaptable(typeof(Vector)));
    }

    [Fact]
    public void Adapter_Source_Has_Setter_Only_For_Writable_Properties()
    {
        var source = new AdapterSourceBuilder().Build(typeof(ICounterView));
        var counter = GeneratedNames.PropertyDependency(typeof(ICounterView), typeof(ICounterView).GetProperty("Counter")!);
        var step = GeneratedNames.PropertyDependency(typeof(ICounterView), typeof(ICounterView).GetProperty("Step")!);

        Assert.Contains($"\"{counter}\"", source);
        Assert.Contains($"\"{counter}.Set\"", source);
        Assert.Contains($"\"{step}\"", source);
        Assert.DoesNotContain($"\"{step}.Set\"", source);
    }

    [Fact]
    public void Strategy_Source_Creates_Adapter_And_Command()
    {
        var source = new StrategySourceBuilder().Build(
            [typeof(ICounterView)], [(typeof(IncrementCommand), typeof(ICounterView))]);

        Assert.Contains("public static object Adapter_SpaceBattle_Tests_Extensions_ICounterViewAdapter(object[] args)", source);
        Assert.Contains("new SpaceBattle.Tests.Extensions.IncrementCommand(App.Ioc.Resolve<SpaceBattle.Tests.Extensions.ICounterView>", source);
    }

    [Fact]
    public void Generated_Adapter_Reads_Property_From_GameItem()
    {
        var item = new Mock<IGameItem>();
        item.Setup(i => i.GetProperty("Counter")).Returns(11);

        var adapter = Ioc.Resolve<ICounterView>(GeneratedNames.AdapterDependency(typeof(ICounterView)), item.Object);

        Assert.Equal(11, adapter.Counter);
    }

    [Fact]
    public void Generated_Command_Uses_The_Configured_Property_Dependency()
    {
        var item = new Mock<IGameItem>();
        item.Setup(i => i.GetProperty("Counter")).Returns(12);
        Ioc.Resolve<App.ICommand>("IoC.Register", "Test.Step", (object[] args) => (object)3).Execute();

        var command = Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(IncrementCommand)), item.Object);
        command.Execute();

        Assert.IsType<IncrementCommand>(command);
        item.Verify(i => i.SetProperty("Counter", 15), Times.Once);
        item.Verify(i => i.GetProperty("Step"), Times.Never);
    }

    [Fact]
    public void Generated_Command_Transfers_A_Property_To_The_Same_GameItem()
    {
        var item = new Mock<IGameItem>();
        item.Setup(i => i.GetProperty("Counter")).Returns(4);

        Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(RecordCounterCommand)), item.Object).Execute();

        item.Verify(i => i.SetProperty("RecordedCounter", 4), Times.Once);
    }

    [Fact]
    public void Compiler_Throws_With_Diagnostics_When_Source_Is_Invalid()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new RoslynSourceCompiler().Compile("class {", Array.Empty<System.Reflection.Assembly>()));

        Assert.Contains("does not compile", error.Message);
    }

    [Fact]
    public void SetPropertyCommand_Writes_Property()
    {
        var item = new Mock<IGameItem>();

        new SetPropertyCommand(item.Object, "Counter", 5).Execute();

        item.Verify(i => i.SetProperty("Counter", 5), Times.Once);
    }

    [Fact]
    public void TypeName_Formats_Generic_And_Array_Types()
    {
        Assert.Equal("System.Int32[]", CSharpTypeName.Format(typeof(int[])));
        Assert.Equal("System.Collections.Generic.IDictionary<System.String, System.Object>",
            CSharpTypeName.Format(typeof(IDictionary<string, object>)));
    }
}
