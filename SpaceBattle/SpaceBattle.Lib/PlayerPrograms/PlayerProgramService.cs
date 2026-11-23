using System.Text;

namespace SpaceBattle.Lib;

public sealed class PlayerProgramService
{
    private readonly IPlayerProgramStore _store;
    private readonly IReadOnlyDictionary<string, IPlayerProgramLanguage> _languages;
    private readonly IPlayerProgramGame _game;
    private readonly IPlayerProgramLimits _limits;

    public PlayerProgramService(IPlayerProgramStore store, IEnumerable<IPlayerProgramLanguage> languages,
        IPlayerProgramGame game, IPlayerProgramLimits limits)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(languages);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(limits);
        _store = store;
        _languages = languages.ToDictionary(language => language.Name, StringComparer.OrdinalIgnoreCase);
        _game = game;
        _limits = limits;
    }

    public IEnumerable<string> Languages => _languages.Keys.Order(StringComparer.Ordinal);

    public void Upload(string player, string language, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(player);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (Encoding.UTF8.GetByteCount(source) > _limits.SourceBytes)
            throw new PlayerProgramException("The program exceeded the source limit.");
        if (!_languages.TryGetValue(language, out var runtime))
            throw new ArgumentException("Unsupported player program language.", nameof(language));
        _store.Save(player, new PlayerProgram(runtime.Name, source));
    }

    public async Task<bool> Run(PlayerIdentity player, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(player);
        cancellationToken.ThrowIfCancellationRequested();
        var program = _store.Find(player.PlayerId) ?? throw new KeyNotFoundException("No player program uploaded.");
        if (!_languages.TryGetValue(program.Language, out var language))
            throw new ProgramSandboxUnavailableException("The saved program language is unavailable.");
        var turn = await _game.ReadTurn(player, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var result = await language.Run(program.Source, turn, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var order = PlayerProgramProtocol.ReadOrder(result, _limits);
        if (order is null) return false;
        _game.SendOrder(player, order);
        return true;
    }

}
