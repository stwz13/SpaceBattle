namespace SpaceBattle.Lib;

public sealed class ObjectAccessScopes : IObjectAccessScopes
{
    private readonly Dictionary<IGameItem, string> _scopes = new(ReferenceEqualityComparer.Instance);

    public void Bind(IGameItem item, string scope)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        _scopes[item] = scope;
    }

    public void Unbind(IGameItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _scopes.Remove(item);
    }

    public string? FindScope(IGameItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _scopes.GetValueOrDefault(item);
    }
}
