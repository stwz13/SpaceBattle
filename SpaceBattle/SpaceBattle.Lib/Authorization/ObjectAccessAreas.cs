namespace SpaceBattle.Lib;

public sealed class ObjectAccessAreas : IObjectAccessAreas
{
    private readonly Dictionary<IGameItem, string> _areas = new(ReferenceEqualityComparer.Instance);

    public void Bind(IGameItem item, string area)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        _areas[item] = area;
    }

    public void Unbind(IGameItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _areas.Remove(item);
    }

    public string? FindArea(IGameItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _areas.GetValueOrDefault(item);
    }
}
