namespace SpaceBattle.Lib;

public interface IFleetRoster
{
    IReadOnlyList<string> Participants { get; }
}
