using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CommandWhitelistTests
{
    [Fact]
    public async Task Whitelist_Applies_To_Ingress_And_To_Restored_Commands()
    {
        await using var fixture = new RuntimeFixture();
        using (fixture.Scope.Enter()) new RegisterIoCDependencyCommandWhitelist(["Record"]).Execute();
        var host = fixture.Runtime.Start("a");
        var receiver = fixture.Resolve<IMessageReceiver>("Messages.ValidatedReceiver");
        Assert.Throws<MessageFormatException>(() => receiver.Receive("{\"gameId\":\"a\",\"command\":\"CreateItem\"}"));
        receiver.Receive("{\"gameId\":\"a\",\"command\":\"Record\",\"args\":7}");
        await host.Enqueue(() => { });
        Assert.Equal(7, Assert.Single(fixture.Seen).Value);
        var interpreter = fixture.Resolve<IMessageInterpreter>("Messages.Interpreter");
        Assert.Throws<MessageFormatException>(() => interpreter.Interpret(new Message("a", "record", default)));
        var restored = fixture.Resolve<IGame>(SerializationDependencies.Restore,
            "{\"gameId\":\"b\",\"items\":[],\"commands\":[{\"type\":\"message\",\"gameId\":\"b\",\"command\":\"Unknown\",\"args\":null}]}");
        var second = fixture.Resolve<ManagedGameHost>("Games.StartRestored", restored);
        await second.Enqueue(() => { });
        Assert.IsType<MessageFormatException>(Assert.Single(fixture.Errors.Handled).Exception);
    }

    [Fact]
    public void Empty_Whitelist_Denies_Everything()
    {
        var whitelist = new CommandWhitelist(new HashSet<string>());
        Assert.False(whitelist.Allows("Record"));
    }
}
