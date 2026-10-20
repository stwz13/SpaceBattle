using SpaceBattle.Lib;

namespace SpaceBattle.Tests.FactoryExtensions;

public class CreateGameItemCommand(IGameItemCreation creation) : ICommand
{
    public void Execute() => creation.CreatedItem = creation.Factory.Create(creation.InitialProperties);
}
