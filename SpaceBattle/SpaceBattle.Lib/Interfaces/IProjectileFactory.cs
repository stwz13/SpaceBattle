namespace SpaceBattle.Lib;

public interface IProjectileFactory
{
    IGameItem Create(Vector position, Angle direction, int speed, int damage);
}
