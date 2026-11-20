using App;
using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class ShipSpeedRule : IGameRuleParameter
{
    public string Name => "Ship.Speed";

    public void Validate(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var speed) || speed < 0)
            throw new ArgumentException("Ship.Speed must be a nonnegative integer.");
    }

    public void Install(JsonElement value)
    {
        Validate(value);
        var speed = value.GetInt32();
        var velocityKey = GeneratedNames.PropertyDependency(typeof(IMoving),
            typeof(IMoving).GetProperty(nameof(IMoving.Velocity))!);
        var parent = new GameScope(Ioc.Resolve<object>("IoC.Scope.Parent"));
        IoCRegistrar.Register(velocityKey, args =>
        {
            if (args is not [IGameItem item]) throw new ArgumentException("Movement requires one item.");
            if (IsShip(item))
                return VelocityCalculation.FromSpeedAndDirection(speed, (Angle)item.GetProperty("Direction"));
            return ScopeExecution.InScope(parent, () => Ioc.Resolve<Vector>(velocityKey, args));
        });
    }

    private static bool IsShip(IGameItem item)
    {
        try { return Equals(item.GetProperty(SerializationDependencies.KindProperty), "Ship"); }
        catch (KeyNotFoundException) { return false; }
    }
}
