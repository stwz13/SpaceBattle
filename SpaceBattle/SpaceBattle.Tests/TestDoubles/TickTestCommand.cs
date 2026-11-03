using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

internal sealed class TickTestCommand(Action execute) : ICommand
{
    public void Execute() => execute();
}
