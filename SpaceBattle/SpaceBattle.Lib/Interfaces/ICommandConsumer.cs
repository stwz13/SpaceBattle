using System.Diagnostics.CodeAnalysis;

namespace SpaceBattle.Lib;

public interface ICommandConsumer
{
    int Count { get; }
    ICommand Take();
    bool TryTake([NotNullWhen(true)] out ICommand? command);
    IEnumerable<ICommand> Consume();
}
