using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class PlayerProgramContractTests
{
    private static readonly PlayerIdentity Player = new("p1", "match", false);

    [Theory]
    [InlineData("python")]
    [InlineData("javascript")]
    [InlineData("replacement")]
    public async Task Language_Implementations_Keep_Identical_Order_And_Failure_Behavior(string language)
    {
        var execution = new Execution();
        var game = new Game();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PlayerPrograms:Limits:OutputBytes"] = "128" }).Build();
        HostComposition.Configure(services, configuration);
        services.AddSingleton<IProgramProcess>(new ProcessAdapter(execution));
        services.AddSingleton<IPlayerProgramLanguage>(new ReplacementLanguage(execution));
        services.AddSingleton<IPlayerProgramGame>(game);
        await using var provider = services.BuildServiceProvider();
        var programs = provider.GetRequiredService<PlayerProgramService>();
        var limits = provider.GetRequiredService<IPlayerProgramLimits>();
        Assert.Equal(128, limits.OutputBytes);
        programs.Upload(Player.PlayerId, language, "first");
        programs.Upload(Player.PlayerId, language, "updated");

        execution.Output = """{"id":"tanker","action":"Refuel","roles":{"Target":"ship"},"playerId":"intruder"}""";
        Assert.True(await programs.Run(Player, default));
        var (identity, order) = Assert.Single(game.Orders);
        Assert.Equal(Player, identity);
        Assert.Equal("tanker", order.Id);
        Assert.Equal("Refuel", order.Action);
        Assert.Equal("ship", order.Roles!["Target"]);
        Assert.Contains("updated", execution.Input);

        execution.Output = "null";
        Assert.False(await programs.Run(Player, default));
        foreach (var invalid in new[]
                 {
                     """{"id":"a","id":"b","action":"Rotation"}""",
                     """{"id":"tanker","action":"Refuel","roles":{" ":"ship"}}""",
                     new string('x', limits.OutputBytes + 1)
                 })
        {
            execution.Output = invalid;
            await Assert.ThrowsAsync<PlayerProgramException>(() => programs.Run(Player, default));
        }
        execution.Failure = new ProgramSandboxUnavailableException("Unavailable");
        await Assert.ThrowsAsync<ProgramSandboxUnavailableException>(() => programs.Run(Player, default));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => programs.Run(Player, cancelled.Token));
        Assert.Single(game.Orders);
    }

    [Fact]
    public async Task Replacing_Storage_Keeps_Programs_Separate_And_Reports_A_Removed_Language()
    {
        var store = new ProgramStore();
        var game = new Game();
        var execution = new Execution { Output = """{"id":"s1","action":"Rotation"}""" };
        var limits = new PlayerProgramLimits(128, 128, 8);
        var programs = new PlayerProgramService(store, [new ReplacementLanguage(execution)], game, limits);
        programs.Upload("p1", "replacement", "p1 source");
        programs.Upload("p2", "replacement", "p2 source");
        Assert.True(await programs.Run(Player, default));
        Assert.Equal("p1 source", store.Find("p1")!.Source);
        Assert.Equal("p2 source", store.Find("p2")!.Source);
        Assert.Throws<PlayerProgramException>(() =>
            programs.Upload("p1", "replacement", new string('x', limits.SourceBytes + 1)));

        var withoutLanguage = new PlayerProgramService(store, [], game, limits);
        await Assert.ThrowsAsync<ProgramSandboxUnavailableException>(() => withoutLanguage.Run(Player, default));
        Assert.Single(game.Orders);
    }

    private sealed class Execution
    {
        public string Input { get; private set; } = "";
        public string Output { get; set; } = "";
        public Exception? Failure { get; set; }

        public Task<string> Run(string input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Input = input;
            return Failure is { } failure ? Task.FromException<string>(failure) : Task.FromResult(Output);
        }
    }

    private sealed class ReplacementLanguage(Execution execution) : IPlayerProgramLanguage
    {
        public string Name => "replacement";

        public Task<string> Run(string source, ProgramTurn turn, CancellationToken cancellationToken) =>
            execution.Run(source, cancellationToken);
    }

    private sealed class ProcessAdapter(Execution execution) : IProgramProcess
    {
        public async Task<(int ExitCode, string Output)> Run(string executable, IEnumerable<string> arguments, string input,
            int outputLimit, CancellationToken cancellationToken) => arguments.First() == "start"
            ? (0, await execution.Run(input, cancellationToken))
            : (0, "");
    }

    private sealed class Game : IPlayerProgramGame
    {
        public List<(PlayerIdentity Player, ProgramOrder Order)> Orders { get; } = new();

        public Task<ProgramTurn> ReadTurn(PlayerIdentity player, CancellationToken cancellationToken) =>
            Task.FromResult(new ProgramTurn(player.GameId, player.PlayerId, []));

        public void SendOrder(PlayerIdentity player, ProgramOrder order) => Orders.Add((player, order));
    }

    private sealed class ProgramStore : IPlayerProgramStore
    {
        private readonly Dictionary<string, PlayerProgram> _data = new(StringComparer.Ordinal);

        public void Save(string player, PlayerProgram program) => _data[player] = program;
        public PlayerProgram? Find(string player) => _data.GetValueOrDefault(player);
    }
}
