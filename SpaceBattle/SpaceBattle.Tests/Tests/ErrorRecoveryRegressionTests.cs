using System.Collections.Concurrent;
using Moq;
using SpaceBattle.Lib;
namespace SpaceBattle.Tests;

public class ErrorRecoveryRegressionTests
{
    [Fact]
    public void Stopping_During_A_Quantum_Leaves_The_Next_Command_Queued()
    {
        var state = new GameExecutionState();
        var scope = new Mock<IGameScope>();
        scope.Setup(value => value.Enter()).Returns(Mock.Of<IDisposable>());
        var quantum = new Mock<IQuantum>();
        quantum.SetupGet(value => value.IsOver).Returns(false);
        var queue = new GameQueue();
        var game = new Game("a", queue, scope.Object, quantum.Object, () => !state.Paused);
        var calls = 0;
        game.Post(new CallbackCommand(() => state.Paused = true));
        game.Post(new CallbackCommand(() => calls++));
        game.Execute();
        Assert.Equal(0, calls);
        Assert.Single(game.Pending);
        state.Paused = false;
        game.Execute();
        Assert.Equal(1, calls);
    }
    [Fact]
    public void Recovery_Uses_The_Injected_Action_And_Records_The_Policy_Decision()
    {
        var log = new MemoryLog(new ConcurrentQueue<(string, Exception, ErrorAction, int)>());
        var action = new Mock<IErrorAction>();
        action.Setup(value => value.Apply("a")).Returns(false);
        var recovery = new ErrorRecovery("a", new ExceptionErrorPolicy(
            new Dictionary<Type, (ErrorAction, int)> { [typeof(IOException)] = (ErrorAction.StopGame, 0) }), log,
            new Dictionary<ErrorAction, IErrorAction> { [ErrorAction.StopGame] = action.Object });
        recovery.Execute(() => throw new IOException("failed"));
        action.Verify(value => value.Apply("a"), Times.Once);
        Assert.Equal(ErrorAction.StopGame, Assert.Single(log.Entries).Action);
    }
}
