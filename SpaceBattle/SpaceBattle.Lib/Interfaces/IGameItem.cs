namespace SpaceBattle.Lib;

public interface IGameItem
{
    object GetProperty(string name);
    void SetProperty(string name, object value);
}
