using SpaceBattle.Lib;

namespace SpaceBattle.Tests.BattleExtensions;

public class CaptureBattleItemCommand(IBattleItemSnapshot view) : ICommand
{
    public void Execute()
    {
        view.BattleId = view.Battle.Id;
        view.SelectedItem = view.Storage.Get(view.ItemId);
    }
}
