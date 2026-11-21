namespace SpaceBattle.Lib;

public interface IFleetOrderActionFactory
{
    ICommand Create(string player, string action, string actor, IReadOnlyDictionary<string, string> roles);
}
