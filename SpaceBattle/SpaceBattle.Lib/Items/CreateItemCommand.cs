namespace SpaceBattle.Lib;

public class CreateItemCommand : ICommand
{
    private readonly IItemCreator _creator;
    private readonly string _kind;
    private readonly string _id;
    private readonly IReadOnlyCollection<KeyValuePair<string, object>> _properties;

    public CreateItemCommand(IItemCreator creator, string kind, string id, IEnumerable<KeyValuePair<string, object>> properties)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(properties);
        _creator = creator;
        _kind = kind;
        _id = id;
        _properties = properties.ToArray();
    }

    public void Execute() => _creator.Create(_kind, _id, _properties);
}
