using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyWorldPhaseConfigurationLoader : ICommand
{
    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register",
        WorldPhaseConfigurationDependencies.Loader, (object[] args) =>
        {
            if (args.Length != 0)
                throw new ArgumentException("The world phase configuration loader takes no arguments.");
            return new JsonWorldPhaseConfigurationLoader();
        }).Execute();
}
