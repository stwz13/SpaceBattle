namespace SpaceBattle.Lib;

public interface IGameItem : IGameItemPropertyReader
{
    void SetProperty(string name, object value);
}
