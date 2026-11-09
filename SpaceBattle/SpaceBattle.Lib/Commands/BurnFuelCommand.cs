namespace SpaceBattle.Lib;

public class BurnFuelCommand : ICommand
{
    private readonly IFuelBurnable _object;

    public BurnFuelCommand(IFuelBurnable obj)
    {
        _object = obj;
    }

    public void Execute()
    {
        _object.Fuel -= _object.FuelConsumption;
    }
}
