namespace SpaceBattle.Lib;

internal sealed class CheckProjectileHitsCommand(
    WorldCollisionFrame frame,
    Func<IGameItem, bool> isProjectile,
    IBattleShipClassifier ships,
    IProjectileCollisionDetector detector,
    Func<IGameItem, IGameItem, ICommand> hit) : ICommand
{
    public void Execute()
    {
        var targets = frame.Items.Where(ships.IsShip).ToArray();
        foreach (var projectile in frame.Items.Where(isProjectile))
        {
            if (!frame.TryGetMovement(projectile, out var path))
                continue;

            var owner = BattleItemProperties.Read<string>(projectile, "Owner");
            var opponents = targets.Where(ship => !ReferenceEquals(ship, projectile)
                    && !BattleItemProperties.IsDestroyed(ship)
                    && (frame.TryGetMovement(ship, out _) || BattleItemProperties.Read<Vector>(ship, "Position") is { Dim: 2 })
                    && BattleItemProperties.Read<string>(ship, "Owner") is string other && other != owner)
                .Select(ship => (ship, frame.PathFor(ship)));
            var target = detector.FirstHit(path, opponents);
            if (target is not null)
                hit(projectile, target).Execute();
        }
    }
}
