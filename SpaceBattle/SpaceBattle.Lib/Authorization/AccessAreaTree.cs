namespace SpaceBattle.Lib;

public sealed class AccessAreaTree : IAccessAreaTree
{
    private readonly Dictionary<string, string[]> _paths = new(StringComparer.Ordinal);

    public void Add(string area, string? parent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        if (_paths.ContainsKey(area))
            throw new ArgumentException("Area already exists.", nameof(area));
        var prefix = parent is null ? Array.Empty<string>() : RequirePath(parent);
        _paths.Add(area, prefix.Append(area).ToArray());
    }

    public void Move(string area, string? parent)
    {
        var previous = RequirePath(area);
        var prefix = parent is null ? Array.Empty<string>() : RequirePath(parent);
        if (prefix.Contains(area, StringComparer.Ordinal))
            throw new InvalidOperationException("A area cannot be moved below itself.");
        var replacement = prefix.Append(area).ToArray();
        foreach (var key in _paths.Keys.ToArray())
        {
            var path = _paths[key];
            if (path.Contains(area, StringComparer.Ordinal))
                _paths[key] = replacement.Concat(path.Skip(previous.Length)).ToArray();
        }
    }

    public IReadOnlyList<string> GetPath(string area)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        return _paths.TryGetValue(area, out var path)
            ? Array.AsReadOnly(path)
            : Array.Empty<string>();
    }

    private string[] RequirePath(string area)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        return _paths.TryGetValue(area, out var path)
            ? path
            : throw new KeyNotFoundException("Unknown access area: " + area);
    }
}
