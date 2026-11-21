namespace SpaceBattle.Lib;

internal sealed class BattleWorldStepCommand : ICommand
{
    private readonly IBattleContext _battle;
    private readonly IBattleShipClassifier _ships;
    private readonly ICommand _projectiles;
    private readonly IBattleProgressTracker _progress;

    public BattleWorldStepCommand(IBattleContext battle, IBattleShipClassifier ships, ICommand projectiles, IBattleProgressTracker progress)
    {
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(projectiles);
        ArgumentNullException.ThrowIfNull(progress);
        _battle = battle;
        _ships = ships;
        _projectiles = projectiles;
        _progress = progress;
    }

    public void Execute()
    {
        var ships = _battle.Storage.GetSnapshot().Where(_ships.IsShip).ToArray();
        _progress.Observe(ships);
        if (!_progress.Current.HasStarted || _progress.Current.IsFinished) return;
        _projectiles.Execute();
        _progress.Observe(ships);
    }
}
