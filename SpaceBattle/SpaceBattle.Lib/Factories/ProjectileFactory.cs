namespace SpaceBattle.Lib;

public class ProjectileFactory : IProjectileFactory
{
    private readonly IGameItemFactory _items;
    private readonly Func<IGameItem, Vector> _velocity;

    public ProjectileFactory(IGameItemFactory items, Func<IGameItem, Vector> velocity)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(velocity);
        _items = items;
        _velocity = velocity;
    }

    public IGameItem Create(Vector position, Angle direction, int speed, int damage)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(direction);
        ArgumentOutOfRangeException.ThrowIfNegative(speed);
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        if (position.Dim != 2)
        {
            throw new ArgumentException("A projectile requires a two-dimensional position.", nameof(position));
        }

        var projectile = _items.Create(new Dictionary<string, object>
        {
            ["Id"] = Guid.NewGuid().ToString("N"),
            ["Position"] = new Vector(position.Coordinates),
            ["Direction"] = direction,
            ["Speed"] = speed,
            ["Damage"] = damage,
            ["Destroyed"] = false
        });
        projectile.SetProperty("Velocity", _velocity(projectile));
        return projectile;
    }
}
