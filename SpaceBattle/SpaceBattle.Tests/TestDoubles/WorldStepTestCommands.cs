using SpaceBattle.Lib;

namespace SpaceBattle.Tests.WorldStepExtensions;

public interface IWorldItemCounter
{
    int Value { get; set; }
}

public sealed class IncrementWorldItemCommand(IWorldItemCounter item) : ICommand
{
    public void Execute() => item.Value++;
}

public interface IWorldFrameSummary
{
    IBattleContext Battle { get; }
    IReadOnlyCollection<IGameItem> Items { get; }
    string Summary { set; }
}

public sealed class SummarizeWorldFrameCommand(IWorldFrameSummary frame) : ICommand
{
    public void Execute() => frame.Summary = frame.Battle.Id + ":" + frame.Items.Count;
}
