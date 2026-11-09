namespace SpaceBattle.Lib;

public sealed class GameTickFactory : IGameTickFactory
{
    private readonly IDelayedCommandScheduler _scheduler;

    public GameTickFactory(IDelayedCommandScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _scheduler = scheduler;
    }

    public IGameTicks Create(TimeSpan step, ICommand worldStep, Action<ICommand> postToGame,
        int maxCatchUpTicks = 2)
    {
        ArgumentNullException.ThrowIfNull(postToGame);
        return new GameTicks(_scheduler, step, worldStep,
            (ticks, generation, number, onDeliveryFailed) => new DeliverGameTickCommand(postToGame,
                new TickCommand(ticks, generation, number), onDeliveryFailed), maxCatchUpTicks);
    }
}
