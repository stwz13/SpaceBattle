using System.Text.Json;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class JsonMessageParserTests
{
    private readonly JsonMessageParser _parser = new();

    [Fact]
    public void Message_Gets_Game_Command_And_Arguments()
    {
        var message = _parser.Parse("""{"gameId":"Battle","command":"Move","args":{"item":"ship","dx":3}}""");

        Assert.Equal("Battle", message.GameId);
        Assert.Equal("Move", message.Command);
        Assert.Equal(JsonValueKind.Object, message.Args.ValueKind);
        Assert.Equal("ship", message.Args.GetProperty("item").GetString());
        Assert.Equal(3, message.Args.GetProperty("dx").GetInt32());
    }

    [Fact]
    public void Arguments_Are_Optional()
    {
        var message = _parser.Parse("""{"gameId":"Battle","command":"Stop"}""");

        Assert.Equal(JsonValueKind.Undefined, message.Args.ValueKind);
    }

    [Fact]
    public void Arguments_Stay_Valid_After_Parsing()
    {
        var message = _parser.Parse("""{"gameId":"A","command":"C","args":[1,2,3]}""");
        GC.Collect();

        Assert.Equal(3, message.Args.GetArrayLength());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"command":"Move"}""")]
    [InlineData("""{"gameId":"Battle"}""")]
    [InlineData("""{"gameId":"","command":"Move"}""")]
    [InlineData("""{"gameId":"  ","command":"Move"}""")]
    [InlineData("""{"gameId":"Battle","command":""}""")]
    [InlineData("""{"gameId":5,"command":"Move"}""")]
    [InlineData("""{"gameId":"Battle","command":null}""")]
    public void Malformed_Messages_Are_Rejected_As_Format_Errors(string text)
    {
        Assert.Throws<MessageFormatException>(() => _parser.Parse(text));
    }

    [Fact]
    public void Null_Text_Is_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!));
    }

    [Fact]
    public void Message_Validates_Its_Fields()
    {
        Assert.Throws<ArgumentException>(() => new Message(" ", "C", default));
        Assert.Throws<ArgumentException>(() => new Message("A", "", default));
        Assert.Throws<ArgumentNullException>(() => new Message(null!, "C", default));
    }
}
