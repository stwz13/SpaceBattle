using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyRouting : ICommand
{
    private readonly int _replicas;
    private readonly IHashFunction _hash;

    public RegisterIoCDependencyRouting(int replicas) : this(replicas, new FnvHashFunction())
    {
    }

    public RegisterIoCDependencyRouting(int replicas, IHashFunction hash)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(replicas, 1);
        ArgumentNullException.ThrowIfNull(hash);
        _replicas = replicas;
        _hash = hash;
    }

    public void Execute()
    {
        var ring = new ConsistentHashRing<IServerThread>(_hash, _replicas);
        Register(RoutingDependencies.Ring, _ => ring);
        Register(RoutingDependencies.Thread, args =>
        {
            if (args.Length != 1 || args[0] is not string gameId)
            {
                throw new ArgumentException("Routing requires the identifier of a game.");
            }
            return Ioc.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring).Find(gameId);
        });
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
