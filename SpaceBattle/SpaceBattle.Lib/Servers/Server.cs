namespace SpaceBattle.Lib;

public class Server : IServer, IServerContext
{
    private readonly object _gate = new();
    private readonly List<Member> _members = [];
    private readonly IConsistentHashRing<IServerThread> _ring;
    private readonly Func<IServerThread> _createThread;
    private readonly Func<IServerThread, ICommand> _softStop;
    private readonly Func<IServerThread, ICommand> _hardStop;
    private readonly int _initialThreads;
    private readonly string _prefix;
    private IServerState _state = CreatedServerState.Instance;
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

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _state.IsRunning;
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

    public void EnsureRunning()
    {
        lock (_gate)
        {
            _state.EnsureRunning();
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            _state.Start(this);
        }
    }

    public IServerThread AddThread()
    {
        lock (_gate)
        {
            return _state.AddThread(this);
        }
    }

    public Task SoftStop() => Stop(thread => thread.Add(_softStop(thread)));

    public Task HardStop() => Stop(thread => thread.Interrupt(_hardStop(thread)));

    private Task Stop(Action<IServerThread> send)
    {
        lock (_gate)
        {
            return _state.Stop(this, send);
        }
    }

    void IServerContext.Become(IServerState state) => _state = state;

    void IServerContext.SpawnInitialThreads()
    {
        foreach (var _ in Enumerable.Range(0, _initialThreads))
        {
            Spawn();
        }
    }

    void IServerContext.RunThreads()
    {
        foreach (var member in _members)
        {
            member.Run = member.Thread.Start();
        }
    }

    IServerThread IServerContext.Spawn() => Spawn().Thread;

    IServerThread IServerContext.SpawnAndRun()
    {
        var member = Spawn();
        member.Run = member.Thread.Start();
        return member.Thread;
    }

    Task IServerContext.Shutdown(Action<IServerThread> send)
    {
        foreach (var member in _members)
        {
            _ring.Remove(member.Id);
            if (member.Run is not null)
            {
                send(member.Thread);
            }
        }
        return Task.WhenAll(_members.Select(member => member.Run).OfType<Task>().ToArray());
    }

    private Member Spawn()
    {
        var member = new Member($"{_prefix}-{_next++}", _createThread());
        _ring.Add(member.Id, member.Thread);
        _members.Add(member);
        return member;
    }

    private sealed class Member(string id, IServerThread thread)
    {
        public string Id { get; } = id;

        public IServerThread Thread { get; } = thread;

        public Task? Run { get; set; }
    }
}
