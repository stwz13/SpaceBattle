using App;
using App.Scopes;
using SpaceBattle.Lib;
using SpaceBattle.Tests.Extensions;

namespace SpaceBattle.Tests;

public class ExtensionTests
{
    public ExtensionTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
    }

    private static void Generate(params Dependency[] dependencies) =>
        new RegisterIoCDependencyGeneratedStrategies(typeof(IncrementCommand).Assembly, dependencies).Execute();

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static void Execute<T>(IGameItem item) =>
        Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(T)), item).Execute();

    [Fact]
    public void Same_Name_Properties_Have_Independent_Strategies_And_Types()
    {
        Generate(new Dependency(typeof(IViewA), "Value", "Test.A"),
            new Dependency(typeof(IViewB), "Value", "Test.B"),
            new Dependency(typeof(ITextView), "Value", "Test.Text"));
        Register("Test.A", _ => 11);
        Register("Test.B", _ => 22);
        Register("Test.Text", _ => "text");
        var item = new TestGameItem();

        Execute<ReadACommand>(item);
        Execute<ReadBCommand>(item);
        Execute<ReadTextCommand>(item);

        Assert.Equal(11, item.Properties["AResult"]);
        Assert.Equal(22, item.Properties["BResult"]);
        Assert.Equal("text", item.Properties["TextResult"]);
    }

    [Fact]
    public void Inherited_Dependency_Is_Used_For_Derived_Interface()
    {
        Generate(new Dependency(typeof(IBaseView), "InheritedValue", "Test.Base"));
        Register("Test.Base", _ => 33);
        var item = new TestGameItem();

        Execute<ReadDerivedCommand>(item);

        Assert.Equal(33, item.Properties["DerivedResult"]);
    }

    [Fact]
    public void Derived_Description_Overrides_Base_Only_For_That_View()
    {
        Generate(new Dependency(typeof(IBaseView), "InheritedValue", "Test.Base"),
            new Dependency(typeof(IDerivedView), "InheritedValue", "Test.Derived"));
        Register("Test.Base", _ => 33);
        Register("Test.Derived", _ => 44);
        var item = new TestGameItem();

        Execute<ReadBaseCommand>(item);
        Execute<ReadDerivedCommand>(item);

        Assert.Equal(33, item.Properties["BaseResult"]);
        Assert.Equal(44, item.Properties["DerivedResult"]);
    }

    [Fact]
    public void Hidden_Property_And_Base_Property_Are_Both_Implemented()
    {
        Generate(new Dependency(typeof(IHiddenBase), "Value", "Test.Int"),
            new Dependency(typeof(IHiddenView), "Value", "Test.String"));
        Register("Test.Int", _ => 5);
        Register("Test.String", _ => "hidden");
        var item = new TestGameItem();

        Execute<ReadHiddenCommand>(item);

        Assert.Equal(5, item.Properties["IntResult"]);
        Assert.Equal("hidden", item.Properties["HiddenResult"]);
    }

    [Fact]
    public void Nested_Generic_Property_Type_Compiles_And_Resolves()
    {
        Generate();
        var nested = new Outer<int>.Inner<string>();
        var item = new TestGameItem();
        item.Properties["Nested"] = nested;
        var view = Ioc.Resolve<INestedView>(GeneratedNames.AdapterDependency(typeof(INestedView)), item);

        Assert.Same(nested, view.Nested);
        Execute<ReadNestedCommand>(item);
    }

    [Fact]
    public void Adding_A_Config_Entry_Extends_An_Action_Without_Changing_Existing_Classes()
    {
        Generate();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        var directory = Directory.CreateTempSubdirectory("SpaceBattle-Extension-");
        try
        {
            var path = Path.Combine(directory.FullName, "Counter.config");
            File.WriteAllText(path, GeneratedNames.CommandDependency(typeof(IncrementCommand)));
            new RegisterIoCDependencyActionConfigurations(directory.FullName).Execute();
            var item = new TestGameItem();
            item.Properties["Counter"] = 1;
            item.Properties["Step"] = 2;
            item.Properties["RecordedCounter"] = 0;

            Ioc.Resolve<Lib.ICommand>("Commands.Action", "Counter", item).Execute();
            Assert.Equal(3, item.Properties["Counter"]);
            Assert.Equal(0, item.Properties["RecordedCounter"]);

            File.AppendAllText(path, "\n" + GeneratedNames.CommandDependency(typeof(RecordCounterCommand)));
            Ioc.Resolve<Lib.ICommand>("Commands.Action", "Counter", item).Execute();

            Assert.Equal(5, item.Properties["Counter"]);
            Assert.Equal(5, item.Properties["RecordedCounter"]);
        }
        finally { directory.Delete(true); }
    }
}
