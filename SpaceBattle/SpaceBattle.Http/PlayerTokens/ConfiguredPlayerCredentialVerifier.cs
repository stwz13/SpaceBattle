using System.Security.Cryptography;
using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal sealed class PlayerAccountOptions
{
    public string PlayerId { get; init; } = "";
    public string PasswordHash { get; init; } = "";
}

internal sealed class ConfiguredPlayerCredentialVerifier : IPlayerCredentialVerifier
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 600_000;
    private static readonly byte[] UnknownAccountHash = new byte[SaltBytes + HashBytes];
    private readonly IReadOnlyDictionary<string, Account> _accounts;

    public ConfiguredPlayerCredentialVerifier(IReadOnlyDictionary<string, PlayerAccountOptions> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        _accounts = accounts.ToDictionary(pair => pair.Key, pair => ReadAccount(pair.Value), StringComparer.Ordinal);
    }

    public ValueTask<string?> Verify(string login, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_accounts.Count == 0) return ValueTask.FromResult<string?>(null);

        _accounts.TryGetValue(login, out var account);
        var stored = account?.PasswordHash ?? UnknownAccountHash;
        Span<byte> derived = stackalloc byte[HashBytes];
        Rfc2898DeriveBytes.Pbkdf2(password.AsSpan(), stored.AsSpan(0, SaltBytes), derived, Iterations, HashAlgorithmName.SHA256);
        var matches = CryptographicOperations.FixedTimeEquals(derived, stored.AsSpan(SaltBytes));
        CryptographicOperations.ZeroMemory(derived);
        return ValueTask.FromResult(matches ? account?.PlayerId : null);
    }

    private static Account ReadAccount(PlayerAccountOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PlayerId);
        var bytes = new byte[SaltBytes + HashBytes];
        if (!Convert.TryFromBase64String(options.PasswordHash, bytes, out var written) || written != bytes.Length)
            throw new ArgumentException("A player password hash must encode a 16-byte salt and a 32-byte hash.");
        return new Account(options.PlayerId, bytes);
    }

    private sealed record Account(string PlayerId, byte[] PasswordHash);
}
