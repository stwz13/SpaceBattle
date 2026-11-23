using System.ComponentModel;
using System.Text;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class DockerPlayerProgramSandboxTests
{
    private static readonly ProgramTurn Turn = new("game", "player", []);
    private static readonly ProgramContainerLaunch Runtime = new("image:version", "dummy", ["argument"]);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly DockerProgramSettings Settings = new("docker", 2, Timeout, Timeout, Timeout, 128, 8, 32, 1, 65534, 65534);
    private static readonly IPlayerProgramLimits Limits = new PlayerProgramLimits(65536, 16384, 8);

    [Fact]
    public async Task Container_Is_Restricted_And_Removed_After_A_Turn()
    {
        var process = new ContainerProcess((_, _) => Task.FromResult((0, "null")));
        using var sandbox = new DockerPlayerProgramSandbox(process, Settings, Limits);
        Assert.Equal("null", await sandbox.Run(Runtime, "source", Turn, default));

        Assert.Equal(new[] { "create", "start", "rm" }, process.Calls.Select(call => call[0]));
        var create = process.Calls[0];
        Assert.Contains("--network=none", create);
        Assert.Contains("--read-only", create);
        Assert.Contains("--user=65534:65534", create);
        Assert.Contains("--cap-drop=ALL", create);
        Assert.Contains("--security-opt=no-new-privileges", create);
        Assert.Contains("--memory=128m", create);
        Assert.Contains("--pids-limit=32", create);
        Assert.Contains("--pull=never", create);
        Assert.DoesNotContain("--privileged", create);
        Assert.DoesNotContain("--volume", create);
        Assert.Equal(process.Calls[1][^1], process.Calls[2][^1]);
    }

    [Fact]
    public async Task Deadline_And_Request_Cancellation_Both_Remove_The_Container()
    {
        static async Task<(int, string)> NeverEnds(string[] _, CancellationToken token)
        {
            await Task.Delay(System.Threading.Timeout.Infinite, token);
            return (0, "null");
        }

        var slow = new ContainerProcess(NeverEnds);
        using (var sandbox = new DockerPlayerProgramSandbox(slow, Settings with { ExecutionTimeout = TimeSpan.FromMilliseconds(100) }, Limits))
        {
            var error = await Assert.ThrowsAsync<PlayerProgramException>(() => sandbox.Run(Runtime, "source", Turn, default));
            Assert.Contains("time limit", error.Message);
            Assert.Equal("rm", slow.Calls[^1][0]);
        }

        using var cancellation = new CancellationTokenSource();
        var cancelled = new ContainerProcess((args, token) =>
        {
            cancellation.Cancel();
            return NeverEnds(args, token);
        });
        using var other = new DockerPlayerProgramSandbox(cancelled, Settings, Limits);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => other.Run(Runtime, "source", Turn, cancellation.Token));
        Assert.Equal("rm", cancelled.Calls[^1][0]);
    }

    [Fact]
    public async Task Nonzero_Exit_Invalid_Text_And_Docker_Error_Are_Reported()
    {
        var failed = new ContainerProcess((_, _) => Task.FromResult((1, "")));
        using var sandbox = new DockerPlayerProgramSandbox(failed, Settings, Limits);
        await Assert.ThrowsAsync<PlayerProgramException>(() => sandbox.Run(Runtime, "source", Turn, default));

        var invalid = new ContainerProcess((_, _) => throw new DecoderFallbackException());
        using var decoding = new DockerPlayerProgramSandbox(invalid, Settings, Limits);
        await Assert.ThrowsAsync<PlayerProgramException>(() => decoding.Run(Runtime, "source", Turn, default));
        Assert.Equal("rm", invalid.Calls[^1][0]);

        var unavailable = new ContainerProcess((_, _) => throw new Win32Exception());
        using var missing = new DockerPlayerProgramSandbox(unavailable, Settings, Limits);
        await Assert.ThrowsAsync<ProgramSandboxUnavailableException>(() => missing.Run(Runtime, "source", Turn, default));

        var removal = new ContainerProcess((_, _) => Task.FromResult((0, "null")), removalExitCode: 1);
        using var unremoved = new DockerPlayerProgramSandbox(removal, Settings, Limits);
        await Assert.ThrowsAsync<ProgramSandboxUnavailableException>(() => unremoved.Run(Runtime, "source", Turn, default));

        var timedOut = new ContainerProcess((_, _) => Task.FromResult((0, "null")), removalError: new OperationCanceledException());
        using var interruptedCleanup = new DockerPlayerProgramSandbox(timedOut, Settings, Limits);
        await Assert.ThrowsAsync<ProgramSandboxUnavailableException>(() => interruptedCleanup.Run(Runtime, "source", Turn, default));
    }

    [Fact]
    public async Task A_Third_Turn_Is_Rejected_While_Two_Are_Running()
    {
        var release = new TaskCompletionSource<(int, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var process = new ContainerProcess((_, _) => release.Task);
        using var sandbox = new DockerPlayerProgramSandbox(process, Settings, Limits);
        var first = sandbox.Run(Runtime, "source", Turn, default);
        var second = sandbox.Run(Runtime, "source", Turn, default);
        await Assert.ThrowsAsync<ProgramSandboxUnavailableException>(() => sandbox.Run(Runtime, "source", Turn, default));
        release.SetResult((0, "null"));
        await Task.WhenAll(first, second).WaitAsync(Timeout);
    }

    private sealed class ContainerProcess(Func<string[], CancellationToken, Task<(int, string)>> execute,
        int removalExitCode = 0, Exception? removalError = null) : IProgramProcess
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string[]> _calls = new();
        public string[][] Calls => _calls.ToArray();

        public Task<(int ExitCode, string Output)> Run(string executable, IEnumerable<string> arguments, string input,
            int outputLimit, CancellationToken cancellationToken)
        {
            var args = arguments.ToArray();
            _calls.Enqueue(args);
            return args[0] switch
            {
                "create" => Task.FromResult((0, "created")),
                "rm" when removalError is not null => Task.FromException<(int, string)>(removalError),
                "rm" => Task.FromResult((removalExitCode, "")),
                _ => execute(args, cancellationToken)
            };
        }
    }
}
