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
