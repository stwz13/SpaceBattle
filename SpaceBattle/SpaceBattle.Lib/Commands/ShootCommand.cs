namespace SpaceBattle.Lib;

public class ShootCommand(IShooting shooting) : ICommand
{
    public void Execute()
    {
        var ammo = shooting.Ammo;
        if (ammo <= 0)
        {
            throw new InvalidOperationException("Not enough ammunition to shoot.");
        }

        var storage = shooting.Storage;
        var projectile = shooting.ProjectileFactory.Create(
            shooting.Position, shooting.Direction, shooting.ProjectileSpeed, shooting.ProjectileDamage);
        storage.Add((string)projectile.GetProperty("Id"), projectile);
        shooting.Ammo = ammo - 1;
    }
}
