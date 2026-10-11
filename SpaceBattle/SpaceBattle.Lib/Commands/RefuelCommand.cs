namespace SpaceBattle.Lib;

public class RefuelCommand : ICommand
{
    private readonly IRefuelable _object;

    public RefuelCommand(IRefuelable obj)
    {
        _object = obj;
    }

    public void Execute()
    {
        _object.FuelReserve -= _object.RefuelAmount;
        _object.Fuel += _object.RefuelAmount;
    }
}
