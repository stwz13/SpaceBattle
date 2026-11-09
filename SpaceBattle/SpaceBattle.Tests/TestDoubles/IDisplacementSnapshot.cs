using SpaceBattle.Lib;

namespace SpaceBattle.Tests.VelocityExtensions;

public interface IDisplacementSnapshot
{
    Vector Displacement { get; }
    Vector DisplacementSnapshot { set; }
}
