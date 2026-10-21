namespace SpaceBattle.Lib;

public class Server : IServer
{
    private readonly object _gate = new();
    private readonly List<Member> _members = [];
    private readonly IConsistentHashRing<IServerThread> _ring;
    private readonly Func<IServerThread> _createThread;
    private readonly Func<IServerThread, ICommand> _softStop;
    private readonly Func<IServerThread, ICommand> _hardStop;
    private readonly int _initialThreads;
    private readonly string _prefix;
    private ServerState _state = ServerState.Created;
    private int _next;

    public Server(IConsistentHashRing<IServerThread> ring, Func<IServerThread> createThread,
        Func<IServerThread, ICommand> softStop, Func<IServerThread, ICommand> hardStop,
        int threadCount, string prefix = "Thread")
    {
        ArgumentNullException.ThrowIfNull(ring);
        ArgumentNullException.ThrowIfNull(createThread);
        ArgumentNullException.ThrowIfNull(softStop);
        ArgumentNullException.ThrowIfNull(hardStop);
        ArgumentOutOfRangeException.ThrowIfLessThan(threadCount, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        _ring = ring;
        _createThread = createThread;
        _softStop = softStop;
        _hardStop = hardStop;
        _initialThreads = threadCount;
        _prefix = prefix;
    }

    public ServerState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public int ThreadCount
    {
        get
        {
            lock (_gate)
            {
                return _members.Count;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            Require(_state == ServerState.Created, "The server has already been started.");
            _state = ServerState.Running;
            foreach (var _ in Enumerable.Range(0, _initialThreads))
            {
                Spawn();
            }
            foreach (var member in _members)
            {
                member.Run = member.Thread.Start();
            }
        }
    }

    public IServerThread AddThread()
    {
        lock (_gate)
        {
            Require(_state is ServerState.Created or ServerState.Running, "The server is stopping.");
            var member = Spawn();
            if (_state == ServerState.Running)
            {
                member.Run = member.Thread.Start();
            }
            return member.Thread;
        }
    }

    public Task SoftStop() => Stop(thread => thread.Add(_softStop(thread)));

    public Task HardStop() => Stop(thread => thread.Interrupt(_hardStop(thread)));

    private Task Stop(Action<IServerThread> send)
    {
        lock (_gate)
        {
            Require(_state is ServerState.Created or ServerState.Running, "The server has already been stopped.");
            _state = ServerState.Stopping;
            foreach (var member in _members)
            {
                _ring.Remove(member.Id);
                if (member.Run is not null)
                {
                    send(member.Thread);
                }
            }
            return Finish(_members.Select(member => member.Run).OfType<Task>().ToArray());
        }
    }

    private async Task Finish(Task[] runs)
    {
        try
        {
            await Task.WhenAll(runs);
        }
        finally
        {
            lock (_gate)
            {
                _state = ServerState.Stopped;
            }
        }
    }

    private Member Spawn()
    {
        var member = new Member($"{_prefix}-{_next++}", _createThread());
        _ring.Add(member.Id, member.Thread);
        _members.Add(member);
        return member;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Member(string id, IServerThread thread)
    {
        public string Id { get; } = id;

        public IServerThread Thread { get; } = thread;

        public Task? Run { get; set; }
    }
}
