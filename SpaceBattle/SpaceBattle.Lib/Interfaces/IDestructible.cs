namespace SpaceBattle.Lib;

public interface IDestructible
{
    int Health { get; }
    bool Destroyed { get; set; }
}
