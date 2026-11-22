namespace SpaceBattle.Lib;

public sealed class GameRulesLobby : IGameRulesLobby, IConfirmedGameStart, IFleetRoster
{
    private readonly object _gate = new();
    private readonly HashSet<string> _participants;
    private readonly GameRuleParameterCatalog _parameters;
    private readonly HashSet<string> _confirmed = new(StringComparer.Ordinal);
    private GameActionRules? _rules;
    private long _revision;
    private IManagedGameHost? _started;

    public GameRulesLobby(string gameId, IEnumerable<string> participants, GameRuleParameterCatalog? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentNullException.ThrowIfNull(participants);
        var roster = participants.ToArray();
        _participants = roster.ToHashSet(StringComparer.Ordinal);
        if (roster.Length == 0 || roster.Length != _participants.Count || roster.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A lobby requires a nonempty roster of distinct player IDs.", nameof(participants));
        GameId = gameId;
        Participants = Array.AsReadOnly(roster);
        _parameters = parameters ?? GameRuleParameterCatalog.Standard;
    }

    public string GameId { get; }
    public IReadOnlyList<string> Participants { get; }

    public GameRulesProposal? ForParticipant(string playerId)
    {
        EnsureParticipant(playerId);
        return Current;
    }

    public GameRulesProposal? Current
    {
        get
        {
            lock (_gate)
                return _rules is null ? null : new GameRulesProposal(_revision, _rules,
                    Array.AsReadOnly(_confirmed.Order(StringComparer.Ordinal).ToArray()), _started is not null);
        }
    }

    public long Propose(string playerId, GameActionRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureParticipant(playerId);
        lock (_gate)
        {
            EnsureOpen();
            _parameters.Validate(rules.Parameters);
            _revision = checked(_revision + 1);
            _rules = rules;
            _confirmed.Clear();
            return _revision;
        }
    }

    public void Confirm(string playerId, long revision)
    {
        EnsureParticipant(playerId);
        lock (_gate)
        {
            EnsureOpen();
            _ = RulesFor(revision);
            _confirmed.Add(playerId);
        }
    }

    IManagedGameHost IConfirmedGameStart.Start(string playerId, long revision, Func<GameActionRules, IManagedGameHost> start)
    {
        ArgumentNullException.ThrowIfNull(start);
        EnsureParticipant(playerId);
        lock (_gate)
        {
            var rules = RulesFor(revision);
            if (_confirmed.Count != _participants.Count)
                throw new InvalidOperationException("All participants must confirm the current rules.");
            return _started ??= start(rules);
        }
    }

    private void EnsureParticipant(string playerId)
    {
        if (!_participants.Contains(playerId))
            throw new UnauthorizedAccessException("Only lobby participants may negotiate or start the game.");
    }

    private void EnsureOpen()
    {
        if (_started is not null) throw new InvalidOperationException("The game has already started.");
    }

    private GameActionRules RulesFor(long revision) => _rules is not null && revision == _revision
        ? _rules : throw new InvalidOperationException("The requested proposal is not current.");
}
