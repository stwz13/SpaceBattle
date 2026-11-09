using System.Collections.Concurrent;
using SpaceBattle.Lib;
namespace SpaceBattle.Tests;

public class ThreadErrorRecoveryTests
{
    [Fact]
    public void Thread_Handler_Retries_And_Logs_The_Actual_Decision()
    {
        var log = new MemoryLog(new ConcurrentQueue<(string, Exception, ErrorAction, int)>());
        var recovery = new ErrorRecovery("server", new ExceptionErrorPolicy(
            new Dictionary<Type, (ErrorAction, int)> { [typeof(IOException)] = (ErrorAction.Retry, 1) }), log,
            new Dictionary<ErrorAction, IErrorAction> { [ErrorAction.Retry] = new RetryErrorAction(), [ErrorAction.Skip] = new SkipErrorAction() });
        var calls = 0;
        new LoggingExceptionHandler(recovery).Handle(new CallbackCommand(() => calls++), new IOException("failed"));
        Assert.Equal(1, calls);
        Assert.Equal(ErrorAction.Retry, Assert.Single(log.Entries).Action);
    }
}
