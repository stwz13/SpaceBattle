namespace SpaceBattle.Lib;

/// <summary>Compensates a specific effect of an existing IoC command during an action transaction.</summary>
public interface ITransactionCommandEffect
{
    string CommandKey { get; }
    ICommand Decorate(object[] args, ICommand command, ITransactionUndo undo);
}