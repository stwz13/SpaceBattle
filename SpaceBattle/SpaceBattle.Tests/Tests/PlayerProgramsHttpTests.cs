using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using App;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class PlayerProgramsHttpTests
{
    private const string GameId = "program-match";
    private const string Run = "/games/" + GameId + "/program/run";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task An_Uploaded_Program_Orders_Its_Own_Ship_Through_The_Existing_Pipeline()
    {
        await using var host = new Factory();
        using var first = Player(host, "p1");
        using var second = Player(host, "p2");
        using var outside = Player(host, "guest");
        using var anonymous = host.CreateClient();
        using var other = Player(host, "p1", "other");
        Assert.Equal(new[] { "javascript", "python" }, await first.GetFromJsonAsync<string[]>("/programs/languages"));
        await Upload(anonymous, "python", "print('null')", HttpStatusCode.Unauthorized);
        await Upload(first, "unsupported", "test", HttpStatusCode.BadRequest);
        await Upload(first, "python", "", HttpStatusCode.BadRequest);
        await Upload(first, "python", new string('x', 65537), HttpStatusCode.RequestEntityTooLarge);
        await Upload(first, "Python", "print('null')", HttpStatusCode.NoContent);
        await Invoke(anonymous, HttpStatusCode.Unauthorized);
        await Invoke(other, HttpStatusCode.Forbidden);
        await Invoke(first, HttpStatusCode.NotFound);

        await Start(first, second);
        await Invoke(first, HttpStatusCode.Forbidden);
        await Invoke(second, HttpStatusCode.NotFound);
        var server = host.Services.GetRequiredService<IServerApplication>();
        var game = Assert.IsAssignableFrom<IManagedGameHost>(server.Runtime.Find(GameId));
        await CreateShip(first, "s1", "p1");
        await CreateShip(second, "s2", "p2");
        await game.Enqueue(() => { }).WaitAsync(Timeout);

        host.Engine.Output = """{"id":"s1","action":"Rotation","playerId":"p2","gameId":"other"}""";
        await Invoke(first, HttpStatusCode.Accepted);
        await game.Enqueue(() =>
        {
            var items = Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage);
            Assert.Equal(new Angle(2), items.Get("s1").GetProperty("Direction"));
            Assert.Equal(new Angle(0), items.Get("s2").GetProperty("Direction"));
        }).WaitAsync(Timeout);
        using var packet = JsonDocument.Parse(host.Engine.Input);
        var turn = packet.RootElement.GetProperty("turn");
        Assert.Equal("p1", turn.GetProperty("playerId").GetString());
        Assert.Equal(GameId, turn.GetProperty("gameId").GetString());
        Assert.Equal("s1", Assert.Single(turn.GetProperty("ships").EnumerateArray()).GetProperty("id").GetString());
        Assert.Contains("python:3.12-alpine", host.Engine.CreateArguments);
        Assert.DoesNotContain("print('null')", host.Engine.CreateArguments);
        Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PlayerPrograms", "Bootstraps", "python.py")),
            host.Engine.CreateArguments[^1]);

        host.Engine.Output = """{"id":"s2","action":"Rotation"}""";
        await Invoke(first, HttpStatusCode.Accepted);
        await game.Enqueue(() => Assert.Equal(new Angle(0),
            Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage).Get("s2").GetProperty("Direction")))
            .WaitAsync(Timeout);

        foreach (var output in new[] { "not json", "[]", """{"id":"s1","action":"Rotation","roles":[]}""",
                     """{"id":"s1","action":"Refuel","roles":{"Target":false}}""" })
        {
            host.Engine.Output = output;
            await Invoke(first, HttpStatusCode.UnprocessableEntity);
        }
        await Upload(second, "javascript", "console.log('null')", HttpStatusCode.NoContent);
        host.Engine.Output = "null";
        await Invoke(second, HttpStatusCode.NoContent);
        Assert.Contains("node:22-alpine", host.Engine.CreateArguments);
        Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PlayerPrograms", "Bootstraps", "javascript.cjs")),
            host.Engine.CreateArguments[^1]);
        host.Engine.CreateExitCode = 125;
        await Invoke(second, HttpStatusCode.ServiceUnavailable);
        await Invoke(outside, HttpStatusCode.NotFound);
    }

    private static async Task Start(HttpClient first, HttpClient second)
    {
        var path = $"/games/{GameId}/rules";
        using var lobby = await first.PostAsJsonAsync(path + "/lobby", new[] { "p1", "p2" });
        lobby.EnsureSuccessStatusCode();
        using var proposal = await first.PutAsJsonAsync(path, new
        {
            parameters = new Dictionary<string, object> { ["objects.Ship"] = new { turnSteps = 2 } }
        });
        proposal.EnsureSuccessStatusCode();
        using var c1 = await first.PostAsync(path + "/1/confirm", null);
        using var c2 = await second.PostAsync(path + "/1/confirm", null);
        c1.EnsureSuccessStatusCode();
        c2.EnsureSuccessStatusCode();
        using var start = await first.PostAsync(path + "/1/start", null);
        start.EnsureSuccessStatusCode();
    }

    private static HttpClient Player(Factory host, string player, string game = GameId)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthenticationTests.Token(player, game));
        return client;
    }

    private static async Task Upload(HttpClient client, string language, string source, HttpStatusCode expected)
    {
        using var text = new StringContent(source);
        using var reply = await client.PutAsync("/players/me/program/" + language, text);
        Assert.Equal(expected, reply.StatusCode);
    }

    private static async Task Invoke(HttpClient client, HttpStatusCode expected)
    {
        using var reply = await client.PostAsync(Run, null);
        Assert.Equal(expected, reply.StatusCode);
    }

    private static async Task CreateShip(HttpClient client, string id, string player)
    {
        using var reply = await client.PostAsJsonAsync("/messages", new
        {
            gameId = GameId,
            command = "CreateItem",
            args = new
            {
                id, kind = "Ship", properties = new Dictionary<string, object>
                {
                    ["Owner"] = new { type = "string", value = player },
                    ["Position"] = new { type = "vector", value = new[] { 0, 0 } },
                    ["Direction"] = new { type = "angle", value = 0 }
                }
            }
        });
        Assert.Equal(HttpStatusCode.Accepted, reply.StatusCode);
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        public DockerDouble Engine { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IProgramProcess>(Engine);
            services.AddSingleton(new ServerOptions
            {
                JwtSecret = AuthenticationTests.Secret,
                AllowedCommands = ["CreateItem", FleetOrderCommand.Name],
                CommandTargets = new() { ["CreateItem"] = "id", [FleetOrderCommand.Name] = "id" },
                Players = new[] { "p1", "p2" }.ToDictionary(player => player, _ =>
                    new PlayerAccessOptions { Commands = ["CreateItem", FleetOrderCommand.Name], Items = ["*"] })
            });
        });
    }

    private sealed class DockerDouble : IProgramProcess
    {
        public string[] CreateArguments { get; private set; } = [];
        public string Input { get; private set; } = "";
        public string Output { get; set; } = "";
        public int CreateExitCode { get; set; }

        public Task<(int ExitCode, string Output)> Run(string executable, IEnumerable<string> arguments, string input,
            int outputLimit, CancellationToken cancellationToken)
        {
            Assert.Equal("docker", executable);
            var args = arguments.ToArray();
            if (args[0] == "create")
            {
                CreateArguments = args;
                return Task.FromResult((CreateExitCode, "id"));
            }
            if (args[0] == "start")
            {
                Input = input;
                return Task.FromResult((0, Output));
            }
            Assert.Equal("rm", args[0]);
            return Task.FromResult((0, ""));
        }
    }
}
