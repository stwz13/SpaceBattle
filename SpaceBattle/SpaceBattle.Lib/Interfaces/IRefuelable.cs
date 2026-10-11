namespace SpaceBattle.Lib;

public interface IRefuelable
{
    int Fuel { get; set; }
    int FuelReserve { get; set; }
    int RefuelAmount { get; }
}
