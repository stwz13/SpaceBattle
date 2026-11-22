namespace SpaceBattle.Lib;

internal sealed class ProjectileFlights(
    Func<IGameItem, bool> canFly, Func<IGameItem, LongOperationCommands> create)
{
    private readonly Dictionary<IGameItem, LongOperationCommands> _active = new(ReferenceEqualityComparer.Instance);

    public void Update(IEnumerable<IGameItem> items)
    {
        var flying = items.Where(canFly).ToHashSet<IGameItem>(ReferenceEqualityComparer.Instance);
        foreach (var removed in _active.Keys.Where(item => !flying.Contains(item)).ToArray())
            Stop(removed);

        foreach (var item in flying)
        {
            if (_active.ContainsKey(item))
                continue;

            var operation = create(item);
            operation.Start.Execute();
            _active.Add(item, operation);
        }
    }

    public void Stop(IGameItem item)
    {
        if (_active.Remove(item, out var operation))
            operation.Stop.Execute();
    }

    public void StopAll()
    {
        foreach (var operation in _active.Values)
            operation.Stop.Execute();

        _active.Clear();
    }
}
