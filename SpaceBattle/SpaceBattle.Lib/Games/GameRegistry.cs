using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

public class GameRegistry : IGameRegistry
{
    private readonly ConcurrentDictionary<string, IGameHost> _hosts = new();

    public void Add(string gameId, IGameHost host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentNullException.ThrowIfNull(host);
        if (!_hosts.TryAdd(gameId, host))
        {
            throw new ArgumentException($"The game \"{gameId}\" is already registered.", nameof(gameId));
        }
    }

    public bool Remove(string gameId)
    {
        ArgumentNullException.ThrowIfNull(gameId);
        return _hosts.TryRemove(gameId, out _);
    }

    public IGameHost Find(string gameId)
    {
        ArgumentNullException.ThrowIfNull(gameId);
        return _hosts.TryGetValue(gameId, out var host)
            ? host
            : throw new KeyNotFoundException($"The game \"{gameId}\" is not running on this server.");
    }
}
