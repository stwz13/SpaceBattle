namespace SpaceBattle.Lib;

internal sealed class TransactionJournal : ITransactionUndo
{
    private Stack<Action>? _undo;
    private TransactionRollbackException? _failure;
    private bool _rollingBack;

    public void Record(Action undo)
    {
        ArgumentNullException.ThrowIfNull(undo);
        EnsureNotRollingBack();
        var active = _undo ?? throw new InvalidOperationException("No game action transaction is active.");
        active.Push(undo);
    }

    public void Execute(Action execute, Action stopGame)
    {
        EnsureNotRollingBack();
        if (_failure is not null)
            throw _failure;

        var root = _undo is null;
        var undo = _undo ??= new Stack<Action>();
        var checkpoint = undo.Count;
        try
        {
            execute();
            if (_failure is not null)
                throw _failure;
        }
        catch (Exception actionError)
        {
            var undoErrors = RollBack(undo, checkpoint);
            if (_failure is not null && !ReferenceEquals(_failure, actionError))
                undoErrors.Add(_failure);
            if (undoErrors.Count > 0)
                _failure = new TransactionRollbackException(actionError, undoErrors);

            if (_failure is not null)
            {
                if (root)
                {
                    try { stopGame(); }
                    catch (Exception error) { _failure = new TransactionRollbackException(_failure, [error]); }
                }
                throw _failure;
            }

            throw;
        }
        finally
        {
            if (root)
                _undo = null;
        }
    }

    private List<Exception> RollBack(Stack<Action> undo, int checkpoint)
    {
        var errors = new List<Exception>();
        _rollingBack = true;
        try
        {
            foreach (var _ in Enumerable.Range(0, undo.Count - checkpoint))
            {
                try { undo.Pop()(); }
                catch (Exception error) { errors.Add(error); }
            }
            return errors;
        }
        finally { _rollingBack = false; }
    }

    private void EnsureNotRollingBack()
    {
        if (_rollingBack)
            throw new InvalidOperationException("Rollback cannot start another transaction or record more undo operations.");
    }
}
