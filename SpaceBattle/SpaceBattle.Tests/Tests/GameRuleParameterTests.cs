using System.Text.Json;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameRuleParameterTests
{
    [Theory]
    [InlineData("""{"speed":8}""")]
    [InlineData("""{"speed":-1,"damage":4}""")]
    [InlineData("""{"speed":8,"damage":1.5}""")]
    [InlineData("""{"speed":8,"damage":4,"unknown":1}""")]
    [InlineData("null")]
    public void Invalid_Projectile_Settings_Cannot_Be_Approved(string json)
    {
        var lobby = new GameRulesLobby("game", ["p"]);
        var rules = Read("{\"parameters\":{\"Shooting.Projectile\":" + json + "}}");
        Assert.Throws<ArgumentException>(() => lobby.Propose("p", rules));
        Assert.Null(lobby.Current);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("3.5")]
    [InlineData("2147483648")]
    [InlineData("\"fast\"")]
    public void Invalid_Speed_Keeps_Current_Version_And_Confirmations(string value)
    {
        var lobby = new GameRulesLobby("game", ["p1"]);
        var original = lobby.Propose("p1", Read("""{"parameters":{"Ship.Speed":0}}"""));
        lobby.Confirm("p1", original);
        var invalid = Read("{\"parameters\":{\"Ship.Speed\":" + value + "}}");
        Assert.Throws<ArgumentException>(() => lobby.Propose("p1", invalid));
        Assert.Equal(original, lobby.Current!.Revision);
        Assert.Equal(new[] { "p1" }, lobby.Current.ConfirmedBy);
    }

    [Fact]
    public void Unknown_Parameter_Survives_The_Input_Document_And_A_New_Handler_Can_Apply_It()
    {
        var rules = Read("""{"parameters":{"Fuel.Capacity":42}}""");
        var lobby = new GameRulesLobby("game", ["p"]);
        lobby.Propose("p", rules);
        var handler = new FuelCapacityRule();
        new GameRuleParameterCatalog([handler]).Install(lobby.Current!.Rules.Parameters);
        Assert.Equal(42, handler.Capacity);
    }

    [Fact]
    public void Legacy_Action_Named_Parameters_Still_Works()
    {
        var rules = Read("""{"parameters":["Mods.Command"]}""");
        Assert.Equal(new[] { "Mods.Command" }, rules.Actions["parameters"]);
        Assert.Empty(rules.Parameters);
    }

    [Fact]
    public void Structured_Actions_Can_Omit_Parameters()
    {
        var rules = Read("""{"actions":{"Rotation":["Mods.Rotate"]}}""");
        Assert.Equal("Mods.Rotate", Assert.Single(rules.Actions["Rotation"]));
        Assert.Empty(rules.Parameters);
    }

    [Theory]
    [InlineData("""{"actions":{"Rotation":["Mods.Rotate"]},"unknown":{}}""", "Unknown field")]
    [InlineData("""{"actions":[],"parameters":{}}""", "Expected objects")]
    [InlineData("""{"actions":{},"parameters":null}""", "Expected objects")]
    public void Structured_Rules_Reject_Unknown_Fields_And_Invalid_Section_Types(string json, string error)
    {
        var exception = Assert.Throws<JsonException>(() => Read(json));
        Assert.Contains(error, exception.Message);
    }

    [Fact]
    public void Property_Bindings_Require_A_Readable_Contract_Of_The_Expected_Type()
    {
        Assert.Throws<ArgumentException>(() => GameObjectRuleProperty<int>.For<GameItem, int>("Speed", (value, _) => value));
        Assert.Throws<ArgumentException>(() => GameObjectRuleProperty<int>.For<IMoving, int>("Missing", (value, _) => value));
        Assert.Throws<ArgumentException>(() => GameObjectRuleProperty<int>.For<IWriteOnlySetting, int>("Speed", (value, _) => value));
        Assert.Throws<ArgumentException>(() =>
            GameObjectRuleProperty<int>.For<IMoving, int>(nameof(IMoving.Velocity), (value, _) => value));
    }

    private interface IWriteOnlySetting
    {
        int Speed { set; }
    }

    private static GameActionRules Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return GameRulesJson.Read(document.RootElement);
    }

    private sealed class FuelCapacityRule : IGameRuleParameter
    {
        public string Name => "Fuel.Capacity";
        public int Capacity { get; private set; }
        public void Validate(JsonElement value) => ArgumentOutOfRangeException.ThrowIfNegative(value.GetInt32());
        public void Install(JsonElement value) => Capacity = value.GetInt32();
    }
}
