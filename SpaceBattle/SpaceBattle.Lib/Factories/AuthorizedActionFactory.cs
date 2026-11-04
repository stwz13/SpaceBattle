namespace SpaceBattle.Lib;

public sealed class AuthorizedActionFactory : IAuthorizedActionFactory
{
    private readonly IAuthorizationPolicy _policy;
    private readonly Func<string, IGameItem, ICommand> _compose;

    public AuthorizedActionFactory(IAuthorizationPolicy policy, Func<string, IGameItem, ICommand> compose)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(compose);
        _policy = policy;
        _compose = compose;
    }

    public ICommand Create(string subject, string action, IGameItem target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(target);
        return new AuthorizedActionCommand(
            () => _policy.IsAllowed(subject, action, target),
            () => _compose(action, target),
            () => throw new UnauthorizedAccessException("The requested action is not permitted."));
    }
}
