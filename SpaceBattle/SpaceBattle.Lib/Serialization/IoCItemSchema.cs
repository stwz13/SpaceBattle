using App;

namespace SpaceBattle.Lib;

public class IoCItemSchema : IItemSchema
{
    public IEnumerable<string> PropertiesOf(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        return Ioc.Resolve<IEnumerable<string>>(SerializationDependencies.Schema(kind));
    }
}
