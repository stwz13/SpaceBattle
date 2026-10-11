using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyCodeGeneration : ICommand
{
    public void Execute()
    {
        var finder = new CommandFinder();
        var adapters = new AdapterSourceBuilder();
        var strategies = new StrategySourceBuilder();
        var compiler = new RoslynSourceCompiler();

        Register("CodeGeneration.CommandFinder", args => finder);
        Register("CodeGeneration.AdapterSourceBuilder", args => adapters);
        Register("CodeGeneration.StrategySourceBuilder", args => strategies);
        Register("CodeGeneration.SourceCompiler", args => compiler);
    }

    private static void Register(string name, Func<object[], object> strategy)
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", name, strategy).Execute();
    }
}
