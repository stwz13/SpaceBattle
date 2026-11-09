namespace SpaceBattle.Lib;

public class CheckFuelCommand : ICommand
{
    private readonly IFuelCheckable _object;

    public CheckFuelCommand(IFuelCheckable obj)
    {
        _object = obj;
    }

    public void Execute()
    {
        if (_object.Fuel < _object.FuelConsumption)
        {
            throw new InvalidOperationException("Not enough fuel to move.");
        }
    }
}
