using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGameRunner : ICommand
{
    public void Execute()
    {
        Register(GameDependencies.Runner, args =>
        {
            if (args.Length != 2 || args[0] is not IGame game || args[1] is not IServerThread thread)
            {
                throw new ArgumentException("A game runner requires a game and the server thread it runs on.");
            }
            return new GameRunner(game, thread);
        });
        Register(GameDependencies.Post, args =>
        {
            if (args.Length != 2 || args[0] is not IGameRunner runner || args[1] is not Lib.ICommand command)
            {
                throw new ArgumentException("Posting requires a game runner and the command to post.");
            }
            return new PostToGameCommand(runner, command);
        });
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
