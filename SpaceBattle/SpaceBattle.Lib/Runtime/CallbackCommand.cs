namespace SpaceBattle.Lib;

public sealed class CallbackCommand(Action action) : ICommand
{
    public void Execute() => action();
}

public sealed class CompletionCommand(Action action, TaskCompletionSource completion) : ICommand
{
    public void Execute()
    {
        try { action(); completion.TrySetResult(); }
        catch (Exception error) { completion.TrySetException(error); }
    }
}
