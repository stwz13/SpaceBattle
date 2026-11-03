using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class MessageDecoratorRegistrationTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("null")]
    [InlineData("extra")]
    public async Task Invalid_Decorator_Does_Not_Replace_Message_Interpreter(string invalidInput)
    {
        await using var fixture = new RuntimeFixture();
        var original = fixture.Resolve<IMessageInterpreter>("Messages.Interpreter");
        object[] arguments = invalidInput switch
        {
            "missing" => [], "null" => [null!], "extra" => [42, 42], _ => [42]
        };
        Assert.Throws<ArgumentException>(() => fixture.Resolve<object>("Messages.Decorate", arguments));
        Assert.Same(original, fixture.Resolve<IMessageInterpreter>("Messages.Interpreter"));
    }
}
