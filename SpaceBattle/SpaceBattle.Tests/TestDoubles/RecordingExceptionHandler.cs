using System.Collections.Concurrent;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class RecordingExceptionHandler : IExceptionHandler
{
    public ConcurrentQueue<(ICommand Command, Exception Exception)> Handled { get; } = new();

    public Action<ICommand, Exception>? Reaction { get; set; }

    public void Handle(ICommand command, Exception exception)
    {
        Handled.Enqueue((command, exception));
        Reaction?.Invoke(command, exception);
    }
}
