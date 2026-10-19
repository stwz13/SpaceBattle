using SpaceBattle.Lib;

namespace SpaceBattle.Tests.FactoryExtensions;

public interface IGameItemCreation
{
    IGameItemFactory Factory { get; }
    IEnumerable<KeyValuePair<string, object>> InitialProperties { get; }
    IGameItem CreatedItem { set; }
}
