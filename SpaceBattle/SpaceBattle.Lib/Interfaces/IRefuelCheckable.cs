namespace SpaceBattle.Lib;

public interface IRefuelCheckable
{
    int FuelReserve { get; }
    int RefuelAmount { get; }
    IExceptionFactory ExceptionFactory { get; }
}
