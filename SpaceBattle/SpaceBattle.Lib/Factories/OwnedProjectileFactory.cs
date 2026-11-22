namespace SpaceBattle.Lib;

internal sealed class OwnedProjectileFactory(IProjectileFactory inner, IGameItem shooter) : IProjectileFactory
{
    public IGameItem Create(Vector position, Angle direction, int speed, int damage)
    {
        var projectile = inner.Create(position, direction, speed, damage);
        projectile.SetProperty("Owner", BattleItemProperties.Read<string>(shooter, "Owner") ?? string.Empty);
        return projectile;
    }
}
