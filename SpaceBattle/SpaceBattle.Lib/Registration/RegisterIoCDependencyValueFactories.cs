using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyValueFactories : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", "Factories.Vector",
            (object[] args) => new VectorFactory()).Execute();
        Ioc.Resolve<App.ICommand>("IoC.Register", "Factories.Angle",
            (object[] args) => new AngleFactory()).Execute();
        Ioc.Resolve<App.ICommand>("IoC.Register", "Utility.Vector",
            (object[] args) => Ioc.Resolve<IVectorFactory>("Factories.Vector").Create((int[])args[0])).Execute();
        Ioc.Resolve<App.ICommand>("IoC.Register", "Utility.Angle",
            (object[] args) => Ioc.Resolve<IAngleFactory>("Factories.Angle").Create((int)args[0])).Execute();
    }
}
