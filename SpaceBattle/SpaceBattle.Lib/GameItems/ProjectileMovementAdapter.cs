namespace SpaceBattle.Lib;

internal sealed class ProjectileMovementAdapter(IGameItem item) : IMoving
{
    public Vector Position
    {
        get => (Vector)item.GetProperty(nameof(Position));
        set => item.SetProperty(nameof(Position), value);
    }

    public Vector Velocity => (Vector)item.GetProperty(nameof(Velocity));
}
