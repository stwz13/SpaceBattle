namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyPriorityThreads(int capacity = int.MaxValue) : ICommand
{
    public void Execute()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        new RegisterIoCDependencyServerThread(capacity, () => CreateQueue(capacity)).Execute();
        RegisterIoCDependencyManagedGames.Register("Queues.Priority", _ => CreateQueue(capacity));
        RegisterIoCDependencyManagedGames.Register("Commands.Priority", args => args is [ICommand command, int priority]
            ? new PrioritizedCommand(command, priority) : throw new ArgumentException("Expected command and priority."));
    }
    private static PriorityCommandQueue CreateQueue(int capacity) =>
        new(capacity, new object(), new SemaphoreSlim(0), new PriorityQueue<ICommand, (long, long)>());
}
