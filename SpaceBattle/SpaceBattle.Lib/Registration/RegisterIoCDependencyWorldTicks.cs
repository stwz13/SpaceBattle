using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyWorldTicks : ICommand
{
    private readonly Func<string[]> _phases;

    public RegisterIoCDependencyWorldTicks() : this(() => Ioc.Resolve<string[]>("Tick.Config")) { }

    internal RegisterIoCDependencyWorldTicks(Func<string[]> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);
        _phases = phases;
    }

    public void Execute()
    {
        IoCRegistrar.Register(GameTickDependencies.Step, args => args.Length == 0
            ? new WorldTickCommand(Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage,
                items => ActionCompositionStrategy.Compose(_phases(), items))
            : throw new ArgumentException("A world tick step takes no arguments."));
        IoCRegistrar.Register(GameTickDependencies.Create, args => args is [ICommandReceiver receiver]
            ? Ioc.Resolve<LongOperationCommands>(LongOperationDependencies.Create,
                Ioc.Resolve<ICommand>(GameTickDependencies.Step), receiver)
            : throw new ArgumentException("A world tick operation requires a command receiver."));
    }
}
