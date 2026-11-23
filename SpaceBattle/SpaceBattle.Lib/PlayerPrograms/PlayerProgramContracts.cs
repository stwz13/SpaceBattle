namespace SpaceBattle.Lib;

public sealed record PlayerProgram(string Language, string Source);

public sealed record ProgramShip(string Id, string Kind, IReadOnlyList<int>? Position, int? Direction, int? Fuel, int? Ammo);

public sealed record ProgramTurn(string GameId, string PlayerId, IReadOnlyList<ProgramShip> Ships);

public sealed record ProgramOrder(string Id, string Action, IReadOnlyDictionary<string, string>? Roles);

public interface IPlayerProgramLimits
{
    int SourceBytes { get; }
    int OutputBytes { get; }
    int JsonDepth { get; }
}

public sealed record PlayerProgramLimits(int SourceBytes, int OutputBytes, int JsonDepth) : IPlayerProgramLimits;

public interface IPlayerProgramStore
{
    /// <summary>Atomically replaces only the program owned by this player.</summary>
    void Save(string player, PlayerProgram program);
    /// <summary>Returns the player's latest saved program, or null if none exists.</summary>
    PlayerProgram? Find(string player);
}

public interface IPlayerProgramLanguage
{
    string Name { get; }
    /// <summary>Runs an isolated turn and returns its UTF-8 stdout as text without dispatching orders.</summary>
    /// <exception cref="PlayerProgramException">The program failed or exceeded an execution limit.</exception>
    /// <exception cref="ProgramSandboxUnavailableException">The execution environment is unavailable.</exception>
    /// <exception cref="OperationCanceledException">The request was cancelled.</exception>
    Task<string> Run(string source, ProgramTurn turn, CancellationToken cancellationToken);
}

public interface IPlayerProgramGame
{
    Task<ProgramTurn> ReadTurn(PlayerIdentity player, CancellationToken cancellationToken);
    /// <summary>Queues one order under the supplied authenticated identity.</summary>
    void SendOrder(PlayerIdentity player, ProgramOrder order);
}
