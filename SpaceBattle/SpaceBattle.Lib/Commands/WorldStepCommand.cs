namespace SpaceBattle.Lib;

public sealed class WorldStepCommand(IWorldStepFactory factory) : ICommand
{
    public void Execute() => factory.Create().Execute();
}
