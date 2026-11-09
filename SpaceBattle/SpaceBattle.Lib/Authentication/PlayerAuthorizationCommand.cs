using System.Text.Json;
namespace SpaceBattle.Lib;

public sealed class PlayerAuthorizationCommand(IMessage message, IMessageInterpreter interpreter,
    IPlayerCommandRules rules, IItemOwners owners) : ICommand
{
    public void Execute()
    {
        if (message is not IAuthenticatedMessage authenticated)
            throw new UnauthorizedAccessException("An authenticated player is required.");
        rules.Validate(authenticated, owners);
        interpreter.Interpret(message).Execute();
    }
}
