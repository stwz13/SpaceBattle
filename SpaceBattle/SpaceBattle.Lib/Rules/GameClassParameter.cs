using System.Text.Json;

namespace SpaceBattle.Lib;

public class GameClassParameter<TSettings> : IGameRuleParameter
{
    private readonly Func<JsonElement, TSettings> _read;
    private readonly Action<TSettings> _install;

    public GameClassParameter(string name, Func<JsonElement, TSettings> read, Action<TSettings> install)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(install);
        Name = name;
        _read = read;
        _install = install;
    }

    public string Name { get; }
    public void Validate(JsonElement value) => _ = Decode(value);
    public void Install(JsonElement value) => _install(Decode(value));

    internal TSettings Decode(JsonElement value)
    {
        try { return _read(value); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException
            or FormatException or OverflowException)
        {
            throw new ArgumentException($"Invalid settings for game rule '{Name}'.", nameof(value), error);
        }
    }
}
