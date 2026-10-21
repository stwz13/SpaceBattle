using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class StubServerThread(string name) : IServerThread
{
    public string Name { get; } = name;

    public int ThreadId => 0;

    public Task Start() => throw new NotSupportedException();

    public void Add(ICommand command) => throw new NotSupportedException();

    public bool TryAdd(ICommand command) => throw new NotSupportedException();

    public void Interrupt(ICommand command) => throw new NotSupportedException();

    public void Schedule(ICommand command) => throw new NotSupportedException();

    public void UpdateBehaviour(Action behaviour) => throw new NotSupportedException();

    public void HardStop() => throw new NotSupportedException();

    public void SoftStop() => throw new NotSupportedException();
}
