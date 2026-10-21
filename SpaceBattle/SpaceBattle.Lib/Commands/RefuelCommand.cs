namespace SpaceBattle.Lib;

public class RefuelCommand : ICommand
{
    private readonly IRefuelTarget _target;
    private readonly IRefuelSource _source;

    public RefuelCommand(IRefuelTarget target, IRefuelSource source)
    {
        _target = target;
        _source = source;
    }

    public void Execute()
    {
        var amount = Math.Min(_source.RefuelAmount, _source.FuelReserve);
        _target.Fuel += amount;
        _source.FuelReserve -= amount;
    }
}
