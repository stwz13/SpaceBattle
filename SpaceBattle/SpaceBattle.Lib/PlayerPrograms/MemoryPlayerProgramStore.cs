using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

public sealed class MemoryPlayerProgramStore : IPlayerProgramStore
{
    private readonly ConcurrentDictionary<string, PlayerProgram> _programs = new(StringComparer.Ordinal);

    public void Save(string player, PlayerProgram program) => _programs[player] = program;

    public PlayerProgram? Find(string player) => _programs.GetValueOrDefault(player);
}
