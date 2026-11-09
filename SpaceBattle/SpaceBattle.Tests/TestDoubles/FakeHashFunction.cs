using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class FakeHashFunction(IReadOnlyDictionary<string, ulong> values) : IHashFunction
{
    public ulong Hash(string key) => values[key];
}
