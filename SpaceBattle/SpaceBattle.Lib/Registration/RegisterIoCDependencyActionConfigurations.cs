using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyActionConfigurations(string directory) : ICommand
{
    public void Execute()
    {
        foreach (var path in Directory.EnumerateFiles(directory, "*.config").OrderBy(p => p, StringComparer.Ordinal))
        {
            var action = Path.GetFileNameWithoutExtension(path);
            Ioc.Resolve<App.ICommand>("IoC.Register", action + ".Config",
                ActionConfigurationStrategy.Create(path)).Execute();
            Ioc.Resolve<App.ICommand>("IoC.Register", "Specs." + action,
                (object[] args) => Ioc.Resolve<string[]>(action + ".Config")).Execute();
        }
    }
}
