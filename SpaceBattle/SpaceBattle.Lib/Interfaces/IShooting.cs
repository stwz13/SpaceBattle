namespace SpaceBattle.Lib;

public interface IShooting
{
    Vector Position { get; }
    Angle Direction { get; }
    int ProjectileSpeed { get; }
    int ProjectileDamage { get; }
    int Ammo { get; set; }
    IProjectileFactory ProjectileFactory { get; }
    IGameItemStorage Storage { get; }
    IExceptionFactory ExceptionFactory { get; }
}
