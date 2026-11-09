namespace SpaceBattle.Tests;

using SpaceBattle.Lib;
using Moq;

public class MacroCommandTest
{
    [Fact]
    public void TestMacroCommandRunsAll()
    {
        Mock<ICommand> cmd1 = new Mock<ICommand>();
        Mock<ICommand> cmd2 = new Mock<ICommand>();
        Mock<ICommand> cmd3 = new Mock<ICommand>();

        ICommand macroCmd = new MacroCommand(new ICommand[] { cmd1.Object, cmd2.Object, cmd3.Object });

        macroCmd.Execute();

        cmd1.Verify(c => c.Execute(), Times.Once());
        cmd2.Verify(c => c.Execute(), Times.Once());
        cmd3.Verify(c => c.Execute(), Times.Once());
    }

    [Fact]
    public void TestMacroCommandThrowsExeptionWhenAnyCommandThrowsException()
    {
        Mock<ICommand> cmd1 = new Mock<ICommand>();
        Mock<ICommand> cmd2 = new Mock<ICommand>();
        cmd2.Setup(c => c.Execute()).Throws<Exception>();
        Mock<ICommand> cmd3 = new Mock<ICommand>();

        ICommand macroCmd = new MacroCommand(new ICommand[] { cmd1.Object, cmd2.Object, cmd3.Object });

        Assert.Throws<Exception>(() => macroCmd.Execute());

        cmd1.Verify(c => c.Execute(), Times.Once());
        cmd2.Verify(c => c.Execute(), Times.Once());
        cmd3.Verify(c => c.Execute(), Times.Never());
    }

    [Fact]
    public void EmptyArrayDoesNotThrowException()
    {
        ICommand macroCmd = new MacroCommand(new ICommand[] { });

        Assert.Null(Record.Exception(() => macroCmd.Execute()));
    }
}
