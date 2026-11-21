using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameRulesLobbyTests
{
    [Theory]
    [InlineData(" ", "Move")]
    [InlineData("Jump", " ")]
    [InlineData("Hit", null)]
    public void Rules_Reject_Blank_Action_Or_Step_Names(string action, string? step)
    {
        Assert.Throws<ArgumentException>(() => new GameActionRules(new Dictionary<string, string[]>
        {
            [action] = [step!]
        }));
    }

    [Fact]
    public void Rules_Reject_Empty_Lists_And_Keep_A_Copy_Of_Submitted_Steps()
    {
        var definitions = new Dictionary<string, string[]> { ["Movement"] = [] };
        Assert.Throws<ArgumentException>(() => new GameActionRules(definitions));
        definitions["Movement"] = ["Move", "BurnFuel"];
        var rules = new GameActionRules(definitions);
        definitions["Movement"][0] = "Shoot";
        definitions.Clear();
        Assert.Equal(new[] { "Move", "BurnFuel" }, rules.Actions["Movement"]);
    }

    [Fact]
    public void Lobby_Rejects_Invalid_Rosters_And_Nonparticipants()
    {
        Assert.Throws<ArgumentException>(() => new GameRulesLobby("game", []));
        Assert.Throws<ArgumentException>(() => new GameRulesLobby("game", ["p1", "p1"]));
        Assert.Throws<ArgumentException>(() => new GameRulesLobby("game", ["p1", " "]));
        var lobby = new GameRulesLobby("game", ["p1"]);
        Assert.Null(lobby.Current);
        Assert.Throws<InvalidOperationException>(() => lobby.Confirm("p1", 1));
        Assert.Throws<UnauthorizedAccessException>(() => lobby.Propose("p2", Movement("Move")));
        var revision = lobby.Propose("p1", Movement("Move"));
        Assert.Throws<UnauthorizedAccessException>(() => lobby.Confirm("p2", revision));
    }

    [Fact]
    public async Task Only_Unanimous_Current_Rules_Start_And_Affect_Their_Game()
    {
        AgreedGameStarter starter = null!;
        var app = ServerComposition.Create(new ServerOptions
        {
            Games = ["default-game"],
            JwtSecret = AuthenticationTests.Secret
        }, configureCommands: () =>
        {
            new RegisterIoCDependencyCodeGeneration().Execute();
            new RegisterIoCDependencyGeneratedStrategies(typeof(MoveCommand).Assembly, []).Execute();
            new RegisterIoCDependencyMacroCommand().Execute();
            new RegisterIoCDependencyActionComposition().Execute();
            new RegisterIoCDependencyRuleCommand("Move", args =>
                Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(MoveCommand)), args)).Execute();
            new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
            starter = GameRulesComposition.CreateStarter();
        });
        var lobby = new GameRulesLobby("custom-game", ["p1", "p2"]);
        var original = lobby.Propose("p1", Movement("Move"));
        lobby.Confirm("p1", original);
        var updated = lobby.Propose("p2", new GameActionRules(new Dictionary<string, string[]>
        {
            ["Movement"] = ["Move", "Move"],
            ["EmergencyJump"] = ["Move", "Mods.Warp.v2"]
        }));

        Assert.Empty(lobby.Current!.ConfirmedBy);
        Assert.Throws<InvalidOperationException>(() => lobby.Confirm("p1", original));
        Assert.Throws<InvalidOperationException>(() => starter.Start(lobby, "p1", original));
        lobby.Confirm("p1", updated);
        lobby.Confirm("p1", updated);
        Assert.Equal(new[] { "p1" }, lobby.Current.ConfirmedBy);
        Assert.Throws<InvalidOperationException>(() => starter.Start(lobby, "p1", updated));
        lobby.Confirm("p2", updated);
        Assert.Throws<UnauthorizedAccessException>(() => starter.Start(lobby, "other", updated));
        Assert.Throws<InvalidOperationException>(() => starter.Start(lobby, "p1", updated));
        Assert.False(lobby.Current.HasStarted);

        await app.Start();
        try
        {
            var started = await Task.WhenAll(Task.Run(() => starter.Start(lobby, "p1", updated)),
                Task.Run(() => starter.Start(lobby, "p2", updated))).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Same(started[0], started[1]);
            Assert.True(lobby.Current.HasStarted);
            Assert.Throws<InvalidOperationException>(() => lobby.Propose("p1", Movement("Rotate")));
            Assert.Throws<InvalidOperationException>(() => lobby.Confirm("p2", updated));

            var custom = started[0];
            var standard = Assert.IsAssignableFrom<IManagedGameHost>(app.Runtime.Find("default-game"));
            await custom.Execute(() => AssertMovement(new Vector(4, 2), 10, ["Move", "Move"]))
                .WaitAsync(TimeSpan.FromSeconds(10));
            await custom.Execute(() =>
            {
                var ship = new GameItem(new Dictionary<string, object>
                    { ["Position"] = new Vector(0, 0), ["Velocity"] = new Vector(2, 1) });
                var missing = Assert.ThrowsAny<Exception>(() =>
                    Ioc.Resolve<Lib.ICommand>("Commands.Action", "EmergencyJump", ship).Execute());
                Assert.Contains("Mods.Warp.v2", missing.ToString());
                Assert.Equal(new Vector(0, 0), ship.GetProperty("Position"));
                new RegisterIoCDependencyRuleCommand("Mods.Warp.v2", args =>
                    new CallbackCommand(() => ((IGameItem)args[0]).SetProperty("Jumped", true))).Execute();
                Ioc.Resolve<Lib.ICommand>("Commands.Action", "EmergencyJump", ship).Execute();
                Assert.Equal(true, ship.GetProperty("Jumped"));
                Assert.Equal(new Vector(2, 1), ship.GetProperty("Position"));
            }).WaitAsync(TimeSpan.FromSeconds(10));
            await standard.Execute(() => AssertMovement(new Vector(2, 1), 9, ["CheckFuel", "Move", "BurnFuel"]))
                .WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { await app.Stop().WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    private static GameActionRules Movement(params string[] steps) =>
        new(new Dictionary<string, string[]> { ["Movement"] = steps });

    private static void AssertMovement(Vector expectedPosition, int expectedFuel, string[] stepNames)
    {
        var keys = Ioc.Resolve<string[]>("Specs.Movement");
        var expectedKeys = stepNames.Length == 2 ? stepNames.Select(name => $"GameRules.Steps.{name}.Create")
            : stepNames.Select(name => $"Commands.SpaceBattle_Lib_{name}Command");
        Assert.Equal(expectedKeys, keys);
        var item = new GameItem(new Dictionary<string, object>
        {
            ["Position"] = new Vector(0, 0), ["Velocity"] = new Vector(2, 1),
            ["Fuel"] = 10, ["FuelConsumption"] = 1
        });
        Ioc.Resolve<Lib.ICommand>("Commands.Action", "Movement", item).Execute();
        Assert.Equal(expectedPosition, item.GetProperty("Position"));
        Assert.Equal(expectedFuel, item.GetProperty("Fuel"));
    }
}
