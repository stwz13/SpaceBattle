using System.Text.Json;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class MessageReceiverTests
{
    private sealed class FakeHost : IGameHost
    {
        public List<IMessage> Sent { get; } = [];

        public void Send(IMessage message) => Sent.Add(message);
    }

    private sealed class FakeInterpreter(Lib.ICommand command) : IMessageInterpreter
    {
        public List<IMessage> Interpreted { get; } = [];

        public Lib.ICommand Interpret(IMessage message)
        {
            Interpreted.Add(message);
            return command;
        }
    }

    private sealed class FakeThread : IServerThread
    {
        public List<Lib.ICommand> Added { get; } = [];

        public bool Full { get; init; }

        public int ThreadId => 0;

        public Task Start() => throw new NotSupportedException();

        public void Add(Lib.ICommand command) => Added.Add(command);

        public bool TryAdd(Lib.ICommand command)
        {
            if (Full)
            {
                return false;
            }
            Added.Add(command);
            return true;
        }

        public void Interrupt(Lib.ICommand command) => throw new NotSupportedException();

        public void Schedule(Lib.ICommand command) => throw new NotSupportedException();

        public void UpdateBehaviour(Action behaviour) => throw new NotSupportedException();

        public void HardStop() => throw new NotSupportedException();

        public void SoftStop() => throw new NotSupportedException();
    }

    private sealed class FakeRunner : IGameRunner
    {
        public List<Lib.ICommand> Posted { get; } = [];

        public void Post(Lib.ICommand command) => Posted.Add(command);

        public void Wake() => throw new NotSupportedException();

        public void Execute() => throw new NotSupportedException();
    }

    private static Message Msg(string game = "A", string command = "Move") => new(game, command, default);

    [Fact]
    public void Received_Message_Is_Sent_To_The_Host_Of_Its_Game()
    {
        var registry = new GameRegistry();
        var host = new FakeHost();
        var other = new FakeHost();
        registry.Add("A", host);
        registry.Add("B", other);
        var receiver = new MessageReceiver(new JsonMessageParser(), registry);

        receiver.Receive("""{"gameId":"A","command":"Move"}""");

        var sent = Assert.Single(host.Sent);
        Assert.Equal("Move", sent.Command);
        Assert.Empty(other.Sent);
    }

    [Fact]
    public void Malformed_Message_Is_Not_Sent_Anywhere()
    {
        var registry = new GameRegistry();
        var host = new FakeHost();
        registry.Add("A", host);
        var receiver = new MessageReceiver(new JsonMessageParser(), registry);

        Assert.Throws<MessageFormatException>(() => receiver.Receive("""{"command":"Move"}"""));
        Assert.Empty(host.Sent);
    }

    [Fact]
    public void Message_For_An_Unknown_Game_Is_Rejected()
    {
        var receiver = new MessageReceiver(new JsonMessageParser(), new GameRegistry());

        Assert.Throws<KeyNotFoundException>(() => receiver.Receive("""{"gameId":"missing","command":"Move"}"""));
    }

    [Fact]
    public void Receiver_Validates_Its_Dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new MessageReceiver(null!, new GameRegistry()));
        Assert.Throws<ArgumentNullException>(() => new MessageReceiver(new JsonMessageParser(), null!));
    }

    [Fact]
    public void Host_Passes_An_Interpreting_Command_To_The_Game_Through_The_Thread()
    {
        var thread = new FakeThread();
        var runner = new FakeRunner();
        var ran = 0;
        var interpreter = new FakeInterpreter(new DelegateStep(() => ran++));
        var message = Msg();
        var host = new GameHost(runner, thread, interpreter);

        host.Send(message);

        Assert.Empty(runner.Posted);
        Assert.Empty(interpreter.Interpreted);
        var post = Assert.Single(thread.Added);
        post.Execute();
        Assert.Empty(interpreter.Interpreted);
        var interpret = Assert.Single(runner.Posted);
        interpret.Execute();
        Assert.Same(message, Assert.Single(interpreter.Interpreted));
        Assert.Equal(1, ran);
    }

    [Fact]
    public void Host_Reports_An_Overload_When_The_Thread_Refuses_The_Message()
    {
        var thread = new FakeThread { Full = true };
        var host = new GameHost(new FakeRunner(), thread, new FakeInterpreter(new DelegateStep(() => { })));

        Assert.Throws<ServerOverloadedException>(() => host.Send(Msg()));
        Assert.Empty(thread.Added);
    }

    [Fact]
    public void Host_Validates_Its_Arguments()
    {
        var runner = new FakeRunner();
        var thread = new FakeThread();
        var interpreter = new FakeInterpreter(new DelegateStep(() => { }));

        Assert.Throws<ArgumentNullException>(() => new GameHost(null!, thread, interpreter));
        Assert.Throws<ArgumentNullException>(() => new GameHost(runner, null!, interpreter));
        Assert.Throws<ArgumentNullException>(() => new GameHost(runner, thread, null!));
        Assert.Throws<ArgumentNullException>(() => new GameHost(runner, thread, interpreter).Send(null!));
    }

    [Fact]
    public void Interpret_Command_Runs_What_The_Interpreter_Returns()
    {
        var ran = 0;
        var interpreter = new FakeInterpreter(new DelegateStep(() => ran++));

        new InterpretMessageCommand(interpreter, Msg()).Execute();

        Assert.Equal(1, ran);
        Assert.Throws<ArgumentNullException>(() => new InterpretMessageCommand(null!, Msg()));
        Assert.Throws<ArgumentNullException>(() => new InterpretMessageCommand(interpreter, null!));
    }

    [Fact]
    public void Failure_While_Interpreting_Is_Not_Swallowed()
    {
        var interpreter = new FakeInterpreter(new DelegateStep(() => throw new InvalidOperationException("boom")));

        Assert.Throws<InvalidOperationException>(() => new InterpretMessageCommand(interpreter, Msg()).Execute());
    }
}
