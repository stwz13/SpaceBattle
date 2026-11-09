namespace SpaceBattle.Lib;

public class Server : IServer, IServerContext, IServerThreadMembership
{
    private readonly object _gate = new();
    private readonly List<ServerThreadMember> _members = [];
    private readonly IConsistentHashRing<IServerThread> _ring;
    private readonly Func<IServerThread> _createThread;
    private readonly Func<IServerThread, ICommand> _softStop;
    private readonly Func<IServerThread, ICommand> _hardStop;
    private readonly int _initialThreads;
    private readonly string _prefix;
    private IServerState _state = CreatedServerState.Instance;
    private int _next;
    private bool _removing;

    public IReadOnlyList<(string Id, IServerThread Thread, Task? Completion)> Threads
    {
        get { lock (_gate) return _members.Select(member => (member.Id, member.Thread, member.Completion)).ToArray(); }
    }

    private readonly ServerThreadRemovalService _threadRemoval;
    public Task RemoveThread(string id, Func<IServerThread, Task> evacuate) => _threadRemoval.Remove(id, evacuate);

    ServerThreadMember IServerThreadMembership.BeginRemoval(string id)
    {
        lock (_gate)
        {
            _state.EnsureRunning();
            Require(!_removing, "The server is not available for thread removal.");
            Require(_members.Count > 1, "The last server thread cannot be removed.");
            var member = _members.SingleOrDefault(candidate => candidate.Id == id)
                ?? throw new KeyNotFoundException($"Thread '{id}' was not found.");
            _removing = true;
            _ring.Remove(id);
            return member;
        }
    }
    void IServerThreadMembership.RollbackRemoval(ServerThreadMember member)
    {
        lock (_gate) _ring.Add(member.Id, member.Thread);
    }
    void IServerThreadMembership.CompleteRemoval(ServerThreadMember member)
    {
        lock (_gate) _members.Remove(member);
    }
    void IServerThreadMembership.EndRemoval() { lock (_gate) _removing = false; }

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
        _threadRemoval = new ServerThreadRemovalService(this, softStop);
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
            Require(!_removing, "Thread removal is in progress.");
            return _state.AddThread(this);
        }
    }

    public Task SoftStop() => Stop(thread => thread.Add(_softStop(thread)));

    public Task HardStop() => Stop(thread => thread.Interrupt(_hardStop(thread)));

    private Task Stop(Action<IServerThread> send)
    {
        lock (_gate)
        {
            Require(!_removing, "Thread removal is in progress.");
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
            member.Completion = member.Thread.Start();
        }
    }

    IServerThread IServerContext.Spawn() => Spawn().Thread;

    IServerThread IServerContext.SpawnAndRun()
    {
        var member = Spawn();
        member.Completion = member.Thread.Start();
        return member.Thread;
    }

    Task IServerContext.Shutdown(Action<IServerThread> send)
    {
        foreach (var member in _members)
        {
            _ring.Remove(member.Id);
            if (member.Completion is not null)
            {
                send(member.Thread);
            }
        }
        return Task.WhenAll(_members.Select(member => member.Completion).OfType<Task>().ToArray());
    }

    private ServerThreadMember Spawn()
    {
        var member = new ServerThreadMember($"{_prefix}-{_next++}", _createThread());
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
}
