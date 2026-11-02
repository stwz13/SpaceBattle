using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyGameTicks : ICommand
{
    private readonly IGameTickFactory _factory;

    public RegisterIoCDependencyGameTicks(IDelayedCommandScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _factory = new GameTickFactory(scheduler);
    }

    public void Execute()
    {
        Register(GameTickDependencies.Factory, args =>
        {
            if (args.Length != 0)
                throw new ArgumentException("The tick factory takes no arguments.");
            return _factory;
        });
        Register(GameTickDependencies.Create, args =>
        {
            if (args.Length is < 3 or > 4 || args[0] is not TimeSpan step
                || args[1] is not ICommand worldStep || args[2] is not Action<ICommand> post
                || (args.Length == 4 && args[3] is not int))
                throw new ArgumentException("Expected step, world step, game post callback and optional catch-up limit.");
            return _factory.Create(step, worldStep, post, args.Length == 4 ? (int)args[3] : 2);
        });
        Register(GameTickDependencies.Start, args => new StartGameTicksCommand(Ticks(args)));
        Register(GameTickDependencies.Stop, args => new StopGameTicksCommand(Ticks(args)));
    }

    private static IGameTicks Ticks(object[] args) => args.Length == 1 && args[0] is IGameTicks ticks
        ? ticks : throw new ArgumentException("Expected one IGameTicks instance.");

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
