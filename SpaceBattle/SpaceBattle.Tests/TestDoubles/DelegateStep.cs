using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class DelegateStep(Action action) : ICommand
{
    public void Execute() => action();
}
