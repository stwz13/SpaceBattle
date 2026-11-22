using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyProjectileFactory : ICommand
{
    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", ShootingDependencies.Factory,
        (object[] args) =>
        {
            IProjectileFactory factory = new ProjectileFactory(
                Ioc.Resolve<IGameItemFactory>(GameItemFactoryDependencies.Factory),
                item => Ioc.Resolve<Vector>(ComputedVelocityDependencies.Velocity, item));
            return args is [IGameItem shooter] ? new OwnedProjectileFactory(factory, shooter) : factory;
        }).Execute();
}
