using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class ShipSpeedRule : GameObjectRuleParameter<int>
{
    public ShipSpeedRule() : base("Ship.Speed", "Ship", ReadSpeed, [Velocity], legacy: true) { }

    private static GameObjectRuleProperty<int> Velocity { get; } =
        GameObjectRuleProperty<int>.For<IMoving, Vector>(nameof(IMoving.Velocity), (speed, item) =>
            VelocityCalculation.FromSpeedAndDirection(speed, (Angle)item.GetProperty("Direction")));

    private static int ReadSpeed(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var speed) || speed < 0)
            throw new ArgumentException("Ship speed must be a nonnegative integer.");
        return speed;
    }
}
