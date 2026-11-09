using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyMessages : ICommand
{
    public void Execute()
    {
        var registry = new GameRegistry();
        var parser = new JsonMessageParser();
        var interpreter = new IoCMessageInterpreter();
        var receiver = new MessageReceiver(parser, registry);
        Register(GameDependencies.Registry, args => Checked(args, 0, "The game registry takes no arguments.", registry));
        Register(MessageDependencies.Parser, args => Checked(args, 0, "The message parser takes no arguments.", parser));
        Register(MessageDependencies.Receiver, args => Checked(args, 0, "The message receiver takes no arguments.", receiver));
        Register(RuntimeDependencies.StartRestored, args =>
        {
            if (args is not [IGame game]) throw new ArgumentException("Expected a restored game.");
            var thread = Ioc.Resolve<IServerThread>(RoutingDependencies.Thread, game.Id);
            var runner = Ioc.Resolve<IGameRunner>(GameDependencies.Runner, game, thread);
            var host = new GameHost(runner, thread, interpreter);
            registry.Add(game.Id, host);
            thread.Add(runner);
            return host;
        });
        Register(GameDependencies.Start, args =>
        {
            if (args.Length != 1 || args[0] is not string gameId)
            {
                throw new ArgumentException("Starting a game requires its identifier.");
            }
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
            var thread = Ioc.Resolve<IServerThread>(RoutingDependencies.Thread, gameId);
            var game = Ioc.Resolve<IGame>(GameDependencies.Create, gameId);
            var runner = Ioc.Resolve<IGameRunner>(GameDependencies.Runner, game, thread);
            var host = new GameHost(runner, thread, interpreter);
            registry.Add(gameId, host);
            return host;
        });
    }

    private static object Checked(object[] args, int expected, string message, object result) =>
        args.Length == expected ? result : throw new ArgumentException(message);

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
