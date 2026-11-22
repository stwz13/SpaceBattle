namespace SpaceBattle.Lib;

public interface IProjectileCollisionDetector
{
    /// <summary>Returns the first target to touch the projectile within the shared tick interval.</summary>
    IGameItem? FirstHit(MovementSegment projectile, IEnumerable<(IGameItem Item, MovementSegment Path)> targets);
}
