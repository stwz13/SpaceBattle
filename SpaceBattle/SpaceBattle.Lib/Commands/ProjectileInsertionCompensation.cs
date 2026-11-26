namespace SpaceBattle.Lib;

internal sealed class ProjectileInsertionCompensation(ICommand command, ITransactionUndo undo, IGameItemStorage storage)
    : ICommand
{
    private readonly ICommand _command = command ?? throw new ArgumentNullException(nameof(command));
    private readonly ITransactionUndo _undo = undo ?? throw new ArgumentNullException(nameof(undo));
    private readonly IGameItemStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    public void Execute()
    {
        var previous = _storage.GetSnapshot().ToHashSet(ReferenceEqualityComparer.Instance);
        var added = Array.Empty<(string Id, IGameItem Item)>();
        _undo.Record(() =>
        {
            foreach (var (id, item) in added.Reverse())
            {
                if (!ReferenceEquals(_storage.Get(id), item) || !_storage.Remove(id))
                    throw new InvalidOperationException($"The projectile '{id}' could not be removed during rollback.");
            }
        });
        try { _command.Execute(); }
        finally
        {
            added = _storage.GetSnapshot().Where(item => !previous.Contains(item))
                .Select(item => ((string)item.GetProperty("Id"), item)).ToArray();
        }
    }
}
