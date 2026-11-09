using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ActionCompositionTests
{
    public ActionCompositionTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
    }

    [Fact]
    public void Config_Files_Define_Order_And_Pass_The_Same_Item_Without_Executing_At_Creation()
    {
        var directory = Directory.CreateTempSubdirectory("SpaceBattle-Config-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "First.config"), "# comment\n Commands.B, Commands.A\n");
            File.WriteAllText(Path.Combine(directory.FullName, "Second.config"), "Commands.A\nCommands.B");
            new RegisterIoCDependencyActionConfigurations(directory.FullName).Execute();
            var item = new Mock<IGameItem>().Object;
            var log = new List<string>();
            var received = new List<object>();
            foreach (var name in new[] { "A", "B" })
            {
                var command = new Mock<Lib.ICommand>();
                command.Setup(c => c.Execute()).Callback(() => log.Add(name));
                Ioc.Resolve<App.ICommand>("IoC.Register", "Commands." + name, (object[] args) =>
                {
                    received.Add(args[0]);
                    return command.Object;
                }).Execute();
            }

            var macro = Ioc.Resolve<Lib.ICommand>("Commands.Action", "First", item);
            Assert.Empty(log);
            Assert.Equal(2, received.Count);
            Assert.All(received, value => Assert.Same(item, value));
            macro.Execute();
            Assert.Equal(new[] { "B", "A" }, log);
            log.Clear();
            Ioc.Resolve<Lib.ICommand>("Commands.Action", "Second", item).Execute();
            Assert.Equal(new[] { "A", "B" }, log);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void Missing_Command_Fails_During_Composition()
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", "Missing.Config", (object[] args) => new[] { "Commands.DoesNotExist" }).Execute();
        Assert.ThrowsAny<Exception>(() => Ioc.Resolve<Lib.ICommand>("Commands.Action", "Missing", new Mock<IGameItem>().Object));
    }

    [Fact]
    public void Empty_Config_Returns_An_Executable_Empty_Macro()
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", "Empty.Config", (object[] args) => Array.Empty<string>()).Execute();
        var command = Ioc.Resolve<Lib.ICommand>("Commands.Action", "Empty", new Mock<IGameItem>().Object);
        Assert.IsType<MacroCommand>(command);
        command.Execute();
    }
}
