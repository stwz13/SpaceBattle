using System.Collections.Concurrent;
namespace SpaceBattle.Lib;

public sealed class ManagedGameRegistry(ConcurrentDictionary<string, IGameHost> hosts) : IManagedGameRegistry
{
    public IReadOnlyCollection<IManagedGameHost> Games => hosts.Values.OfType<IManagedGameHost>().ToArray();
    public IGameHost Find(string id) => hosts.TryGetValue(id, out var host)
        ? host : throw new KeyNotFoundException($"Game '{id}' was not found.");
    public IManagedGameHost FindManaged(string id) => Find(id) is IManagedGameHost host
        ? host : throw new InvalidOperationException($"Game '{id}' is not managed.");
    public bool Contains(string id) => hosts.ContainsKey(id);
    public void Add(string id, IGameHost host)
    {
        if (!hosts.TryAdd(id, host)) throw new ArgumentException($"Game '{id}' already exists.");
    }
    public bool Remove(string id) => hosts.TryRemove(id, out _);
}
