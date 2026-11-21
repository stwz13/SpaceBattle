namespace SpaceBattle.Lib;

internal sealed class BattleTickDriver(IGameTickFactory factory, TimeSpan step, Action<Exception> report)
{
    public Action Attach(IManagedGameHost host, ICommand stepCommand, Func<bool> finished)
    {
        IGameTicks? ticks = null;
        var world = new CallbackCommand(() =>
        {
            if (host.State == GameHostState.Running && !host.Game.Paused)
                stepCommand.Execute();
            if (finished()) ticks!.Stop();
        });
        ticks = factory.Create(step, world, command =>
        {
            if (host.State is GameHostState.Stopped or GameHostState.Moved)
            {
                ticks!.Stop();
                return;
            }
            _ = Deliver(host, command, ticks!);
        });
        return () =>
        {
            if (!finished()) ticks.Start();
        };
    }

    private async Task Deliver(IManagedGameHost host, ICommand command, IGameTickControl ticks)
    {
        try { await host.Execute(command.Execute).ConfigureAwait(false); }
        catch (Exception error)
        {
            ticks.Stop();
            report(error);
        }
    }
}
