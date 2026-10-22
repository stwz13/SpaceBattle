using App;
using App.Scopes;
using SpaceBattle.Lib;
using SpaceBattle.Tests.Extensions;

namespace SpaceBattle.Tests;

public class GeneratedPreparationCompatibilityTests
{
    public GeneratedPreparationCompatibilityTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
    }

    private static void Prepare(int mode, Type[] interfaces, Dependency[] dependencies)
    {
        switch (mode)
        {
            case 0:
                new RegisterIoCDependencyGeneratedStrategies(typeof(ReadHiddenCommand).Assembly, dependencies).Execute();
                break;
            case 1:
                new RegisterIoCDependencyGeneratedStrategiesWithAdapters([typeof(ReadHiddenCommand).Assembly],
                    interfaces, dependencies).Execute();
                break;
            default:
                new RegisterIoCDependencyGeneratedAdapters(interfaces, dependencies).Execute();
                break;
        }
    }

    private static T Adapt<T>(IGameItem item) =>
        Ioc.Resolve<T>(GeneratedNames.AdapterDependency(typeof(T)), item);

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void All_Entry_Points_Preserve_Inherited_And_Hidden_Property_Bindings(int mode)
    {
        Prepare(mode, [typeof(IBaseView), typeof(IDerivedView), typeof(IHiddenView)],
            [new Dependency(typeof(IBaseView), "InheritedValue", "Test.Base"),
             new Dependency(typeof(IDerivedView), "InheritedValue", "Test.Derived"),
             new Dependency(typeof(IHiddenBase), "Value", "Test.Int"),
             new Dependency(typeof(IHiddenView), "Value", "Test.Text")]);
        Register("Test.Base", _ => 11);
        Register("Test.Derived", _ => 22);
        Register("Test.Int", _ => 33);
        Register("Test.Text", _ => "text");
        var item = new TestGameItem();

        Assert.Equal(11, Adapt<IBaseView>(item).InheritedValue);
        Assert.Equal(22, Adapt<IDerivedView>(item).InheritedValue);
        var hidden = Adapt<IHiddenView>(item);
        Assert.Equal("text", hidden.Value);
        Assert.Equal(33, ((IHiddenBase)hidden).Value);
        hidden.IntResult = 44;
        hidden.HiddenResult = "saved";
        Assert.Equal(44, item.GetProperty("IntResult"));
        Assert.Equal("saved", item.GetProperty("HiddenResult"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void All_Entry_Points_Preserve_Default_Reads_Writes_And_Forwarding_Of_Source_Items(int mode)
    {
        Prepare(mode, [typeof(ICounterView)],
            [new Dependency(typeof(ICounterView), nameof(ICounterView.Step), "Test.Step")]);
        var item = new TestGameItem();
        item.SetProperty("Counter", 1);
        item.SetProperty("Multiplier", 3);
        Register("Test.Step", args => (int)((IGameItem)args[0]).GetProperty("Multiplier") * 2);
        var view = Adapt<ICounterView>(item);

        view.Counter += view.Step;
        item.SetProperty("Multiplier", 4);
        view.Counter += view.Step;

        Assert.Equal(15, item.GetProperty("Counter"));
        Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Step"));
    }
}
