namespace SpaceBattle.Lib;

public class GameItem : IGameItem
{
    private readonly IDictionary<string, object> _properties;

    public GameItem(IDictionary<string, object> properties)
    {
        _properties = properties;
    }

    public object GetProperty(string name) => _properties[name];

    public void SetProperty(string name, object value) => _properties[name] = value;
}
