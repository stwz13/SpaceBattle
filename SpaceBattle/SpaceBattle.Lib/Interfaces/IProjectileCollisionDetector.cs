namespace SpaceBattle.Lib;

public interface IProjectileCollisionDetector
{
    /// <summary>Finds the nearest target intersecting the closed 2D segment without changing any item.</summary>
    /// <exception cref="ArgumentException">The start or end vector is not two-dimensional.</exception>
    IGameItem? FirstHit(Vector start, Vector end, IEnumerable<IGameItem> targets);
}
