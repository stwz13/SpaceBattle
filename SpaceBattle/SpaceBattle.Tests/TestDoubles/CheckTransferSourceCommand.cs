using SpaceBattle.Lib;

namespace SpaceBattle.Tests.ContextExtensions;

public class CheckTransferSourceCommand(ITransferSource source, IExceptionFactory exceptions) : ICommand
{
    public void Execute()
    {
        if (source.Amount <= 0)
        {
            throw exceptions.InvalidOperation("The source must have a positive amount.");
        }
    }
}
