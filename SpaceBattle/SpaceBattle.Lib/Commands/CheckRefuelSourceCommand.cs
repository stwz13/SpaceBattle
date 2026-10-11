namespace SpaceBattle.Lib;

public class CheckRefuelSourceCommand : ICommand
{
    private readonly IRefuelCheckable _object;

    public CheckRefuelSourceCommand(IRefuelCheckable obj)
    {
        _object = obj;
    }

    public void Execute()
    {
        if (_object.FuelReserve < _object.RefuelAmount)
        {
            throw _object.ExceptionFactory.InvalidOperation("Not enough fuel in reserve to refuel.");
        }
    }
}
