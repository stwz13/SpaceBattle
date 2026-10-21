using SpaceBattle.Lib;

namespace SpaceBattle.Tests.BattleExtensions;

public interface IBattleItemSnapshot
{
    IBattleContext Battle { get; }
    IGameItemStorage Storage { get; }
    string ItemId { get; }
    string BattleId { set; }
    IGameItem SelectedItem { set; }
}
