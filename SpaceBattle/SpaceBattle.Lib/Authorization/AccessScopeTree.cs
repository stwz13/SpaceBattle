namespace SpaceBattle.Lib;

public sealed class AccessScopeTree : IAccessScopeTree
{
    private readonly Dictionary<string, string[]> _paths = new(StringComparer.Ordinal);

    public void Add(string scope, string? parent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        if (_paths.ContainsKey(scope))
            throw new ArgumentException("Scope already exists.", nameof(scope));
        var prefix = parent is null ? Array.Empty<string>() : RequirePath(parent);
        _paths.Add(scope, prefix.Append(scope).ToArray());
    }

    public void Move(string scope, string? parent)
    {
        var previous = RequirePath(scope);
        var prefix = parent is null ? Array.Empty<string>() : RequirePath(parent);
        if (prefix.Contains(scope, StringComparer.Ordinal))
            throw new InvalidOperationException("A scope cannot be moved below itself.");
        var replacement = prefix.Append(scope).ToArray();
        foreach (var key in _paths.Keys.ToArray())
        {
            var path = _paths[key];
            if (path.Contains(scope, StringComparer.Ordinal))
                _paths[key] = replacement.Concat(path.Skip(previous.Length)).ToArray();
        }
    }

    public IReadOnlyList<string> GetPath(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        return _paths.TryGetValue(scope, out var path)
            ? Array.AsReadOnly(path)
            : Array.Empty<string>();
    }

    private string[] RequirePath(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        return _paths.TryGetValue(scope, out var path)
            ? path
            : throw new KeyNotFoundException("Unknown access scope: " + scope);
    }
}
