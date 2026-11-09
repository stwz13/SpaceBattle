using SpaceBattle.Lib;

namespace SpaceBattle.Tests.VelocityExtensions;

public interface IVelocitySnapshot
{
    Vector Velocity { get; }
    Vector VelocitySnapshot { set; }
}
