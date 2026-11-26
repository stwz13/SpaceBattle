using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class PlayerProgramProcessTests
{
    [Fact]
    public async Task Process_Output_Is_Bounded_And_Cancelled_Processes_Are_Stopped()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json")).Build();
        HostComposition.Configure(services, configuration);
        await using var provider = services.BuildServiceProvider();
        var process = provider.GetRequiredService<IProgramProcess>();
        var (shell, echo, sleep) = OperatingSystem.IsWindows()
            ? ("cmd.exe", new[] { "/d", "/c", "echo", "player-program" },
                new[] { "/d", "/c", "ping -n 90 127.0.0.1 >nul" })
            : ("/bin/sh", new[] { "-c", "printf player-program" }, new[] { "-c", "sleep 90" });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await process.Run(shell, echo, "", 64, deadline.Token);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("player-program", result.Output.Trim());
        await Assert.ThrowsAsync<PlayerProgramException>(() => process.Run(shell, echo, "", 4, deadline.Token));
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => process.Run(shell, sleep, "", 64, cancel.Token));
    }
}
