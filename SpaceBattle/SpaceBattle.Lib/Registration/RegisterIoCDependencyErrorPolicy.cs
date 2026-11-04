using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyErrorPolicy(
    IReadOnlyDictionary<Type, (ErrorAction Action, int Retries)> rules, ILog? log = null) : ICommand
{
    public void Execute()
    {
        if (rules.Any(rule => rule.Value.Retries < 0 || !typeof(Exception).IsAssignableFrom(rule.Key)))
            throw new ArgumentException("Error rules require exception types and nonnegative retry limits.");
        var policy = new ExceptionErrorPolicy(rules.ToDictionary());
        var output = log ?? new MemoryLog(new ConcurrentQueue<(string, Exception, ErrorAction, int)>());
        var runtime = Ioc.Resolve<IGameRuntime>("Server.Games");
        var ring = Ioc.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring);
        RegisterIoCDependencyManagedGames.Register("Errors.Policy", _ => policy);
        RegisterIoCDependencyManagedGames.Register("Errors.Log", _ => output);
        Ioc.Resolve<IMessageInterpreter>("Messages.Decorate", (Func<IMessageInterpreter, IMessageInterpreter>)(inner =>
            new PolicyMessageInterpreter(inner, (message, action) => new ErrorPolicyCommand(message.GameId, action, policy, output,
                () => ((IManagedGameHost)runtime.Find(message.GameId)).StopForError(),
                () =>
                {
                    var owner = ((IManagedGameHost)runtime.Find(message.GameId)).Thread;
                    runtime.Games.Where(game => ReferenceEquals(game.Thread, owner)).ToList().ForEach(game => game.StopForError());
                    ((Server)runtime.Server).Threads.Where(member => ReferenceEquals(member.Thread, owner)).ToList()
                        .ForEach(member => ring.Remove(member.Id));
                    owner.HardStop();
                }))));
    }
}
