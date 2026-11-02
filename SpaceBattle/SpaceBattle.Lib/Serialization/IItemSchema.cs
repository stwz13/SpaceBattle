namespace SpaceBattle.Lib;

public interface IItemSchema
{
    IEnumerable<string> PropertiesOf(string kind);
}
