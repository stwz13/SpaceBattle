namespace SpaceBattle.Lib;

public interface IRefuelSource
{
    int FuelReserve { get; set; }
    int RefuelAmount { get; }
}
