using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class TestGameItem : IGameItem
{
    public readonly Dictionary<string, object> Properties = new();
    public object GetProperty(string name) => Properties[name];
    public void SetProperty(string name, object value) => Properties[name] = value;
}
