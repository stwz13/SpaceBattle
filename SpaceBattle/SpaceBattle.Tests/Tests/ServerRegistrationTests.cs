using System.Collections.Concurrent;
using System.Text.Json;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ServerRegistrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly RecordingExceptionHandler _handler = new();
    private readonly ConcurrentQueue<string> _seen = new();

    public ServerRegistrationTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyExceptionFactory().Execute();
        Register(ServerThreadDependencies.ExceptionHandler, _ => _handler);
        new RegisterIoCDependencyBattleContextFactory().Execute();
        new RegisterIoCDependencyGameQuantum(TimeSpan.FromHours(1)).Execute();
        new RegisterIoCDependencyGame().Execute();
        new RegisterIoCDependencyServerThread().Execute();
        new RegisterIoCDependencyGameRunner().Execute();
        new RegisterIoCDependencyRouting(16).Execute();
        new RegisterIoCDependencyMessages().Execute();
        Register(MessageDependencies.CommandKey("Echo"), args =>
            new DelegateStep(() => _seen.Enqueue(((IMessage)args[0]).Args.GetProperty("text").GetString()!)));
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static IServer Server() => Ioc.Resolve<IServer>(ServerDependencies.Instance);

    private static void Run(string key) => Ioc.Resolve<Lib.ICommand>(key).Execute();

    [Fact]
    public async Task Server_Is_One_Shared_Object_With_The_Configured_Number_Of_Threads()
    {
        new RegisterIoCDependencyServer(7).Execute();

        Assert.Same(Server(), Server());
        Assert.False(Server().IsRunning);

        Run(ServerDependencies.Start);

        Assert.Equal(7, Server().ThreadCount);
        Assert.Equal(7, Ioc.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring).Count);
        await Server().SoftStop().WaitAsync(Timeout);
    }

    [Fact]
    public void Server_Handles_Messages_From_Start_To_Soft_Stop()
    {
        new RegisterIoCDependencyServer(4).Execute();
        Run(ServerDependencies.Start);
        var games = Enumerable.Range(0, 20).Select(index => $"Battle-{index}").ToArray();
        foreach (var game in games)
        {
            Ioc.Resolve<IGameHost>(GameDependencies.Start, game);
        }
        var receiver = Ioc.Resolve<IMessageReceiver>(MessageDependencies.Receiver);

        foreach (var game in games)
        {
            receiver.Receive(JsonSerializer.Serialize(new { gameId = game, command = "Echo", args = new { text = game } }));
        }
        Run(ServerDependencies.SoftStop);
        Assert.True(SpinWait.SpinUntil(() => _seen.Count == games.Length, Timeout));

        Assert.False(Server().IsRunning);

        Assert.Equal(games.Order(), _seen.Order());
        Assert.Empty(_handler.Handled.ToArray());
    }

    [Fact]
    public async Task Server_Can_Be_Started_From_A_Thread_That_Has_No_Scope_Of_Its_Own()
    {
        new RegisterIoCDependencyServer(2).Execute();
        var server = Server();

        await Task.Run(server.Start).WaitAsync(Timeout);

        Assert.Equal(2, server.ThreadCount);
        await server.SoftStop().WaitAsync(Timeout);
    }

    [Fact]
    public void Hard_Stop_Command_Stops_The_Server()
    {
        new RegisterIoCDependencyServer(3).Execute();
        Run(ServerDependencies.Start);

        Run(ServerDependencies.HardStop);

        Assert.False(Server().IsRunning);
        Assert.Equal(0, Ioc.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring).Count);
    }

    [Fact]
    public void Server_Dependencies_Take_No_Arguments()
    {
        new RegisterIoCDependencyServer(1).Execute();

        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IServer>(ServerDependencies.Instance, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(ServerDependencies.Start, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(ServerDependencies.SoftStop, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(ServerDependencies.HardStop, 1));
    }

    [Fact]
    public void Thread_Count_Must_Be_Positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegisterIoCDependencyServer(0));
    }

    [Fact]
    public void Management_Commands_Validate_The_Server()
    {
        Assert.Throws<ArgumentNullException>(() => new StartServerCommand(null!));
        Assert.Throws<ArgumentNullException>(() => new SoftStopServerCommand(null!));
        Assert.Throws<ArgumentNullException>(() => new HardStopServerCommand(null!));
    }
}
