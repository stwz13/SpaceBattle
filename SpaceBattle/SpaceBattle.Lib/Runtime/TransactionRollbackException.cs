namespace SpaceBattle.Lib;

public sealed class TransactionRollbackException(Exception actionError, IEnumerable<Exception> undoErrors)
    : AggregateException("The game action failed and its changes could not be fully rolled back.",
        undoErrors.Prepend(actionError));
