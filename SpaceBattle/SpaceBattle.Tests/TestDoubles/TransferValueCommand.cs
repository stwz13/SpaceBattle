using SpaceBattle.Lib;

namespace SpaceBattle.Tests.ContextExtensions;

public class TransferValueCommand(ITransferTarget target, ITransferSource source) : ICommand
{
    public void Execute()
    {
        target.Amount += source.Amount;
        source.Amount = 0;
    }
}
