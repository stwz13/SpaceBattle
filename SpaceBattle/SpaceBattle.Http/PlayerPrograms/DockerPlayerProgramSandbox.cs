using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using SpaceBattle.Lib;

namespace SpaceBattle.Http;

public sealed class DockerPlayerProgramSandbox : IPlayerProgramSandbox, IDisposable
{
    private readonly IProgramProcess _process;
    private readonly SemaphoreSlim _slots;
    private readonly DockerProgramSettings _settings;
    private readonly IPlayerProgramLimits _limits;

    public DockerPlayerProgramSandbox(IProgramProcess process, DockerProgramSettings settings, IPlayerProgramLimits limits)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Executable);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(settings.ExecutionTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(settings.CleanupTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.ParallelRuns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.MemoryMiB);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.TmpfsMiB);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.ProcessLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.Cpus);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.UserId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.GroupId);
        _process = process;
        _slots = new SemaphoreSlim(settings.ParallelRuns);
        _settings = settings;
        _limits = limits;
    }

    public async Task<string> Run(ProgramContainerLaunch launch, string source, ProgramTurn turn, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_slots.Wait(0)) throw new ProgramSandboxUnavailableException("All program slots are in use.");
        try { return await RunContainer(launch, source, turn, cancellationToken); }
        finally { _slots.Release(); }
    }

    private async Task<string> RunContainer(ProgramContainerLaunch launch, string source, ProgramTurn turn,
        CancellationToken cancellationToken)
    {
        var name = "spacebattle-program-" + Guid.NewGuid().ToString("N");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_settings.ExecutionTimeout);
        var created = false;
        try
        {
            var create = await Docker(
                ["create", "--interactive", "--name", name, "--pull=never", "--platform=linux", "--network=none",
                 "--read-only", "--tmpfs", $"/tmp:rw,noexec,nosuid,nodev,size={_settings.TmpfsMiB}m,mode=1777",
                 $"--user={_settings.UserId}:{_settings.GroupId}",
                 "--cap-drop=ALL", "--security-opt=no-new-privileges", $"--memory={_settings.MemoryMiB}m",
                 $"--memory-swap={_settings.MemoryMiB}m", "--cpus", _settings.Cpus.ToString(CultureInfo.InvariantCulture),
                 $"--pids-limit={_settings.ProcessLimit}", "--log-driver=none", "--entrypoint", launch.EntryPoint,
                 launch.Image, .. launch.Arguments], "", deadline.Token);
            if (create.ExitCode != 0)
                throw new ProgramSandboxUnavailableException("Docker or the configured program image is unavailable.");
            created = true;

            var input = JsonSerializer.Serialize(new { source, turn }, JsonSerializerOptions.Web);
            var result = await Docker(["start", "--attach", "--interactive", name], input, deadline.Token);
            if (result.ExitCode != 0)
                throw new PlayerProgramException("The player program did not complete successfully.");
            return result.Output;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PlayerProgramException("The player program exceeded its time limit.");
        }
        catch (DecoderFallbackException)
        {
            throw new PlayerProgramException("The player program must output UTF-8 JSON.");
        }
        catch (Win32Exception error)
        {
            throw new ProgramSandboxUnavailableException("The Docker client is unavailable.", error);
        }
        finally
        {
            if (created) await Remove(name);
        }
    }

    private async Task Remove(string name)
    {
        using var cleanup = new CancellationTokenSource(_settings.CleanupTimeout);
        try
        {
            var removed = await Docker(["rm", "--force", name], "", cleanup.Token);
            if (removed.ExitCode != 0)
                throw new ProgramSandboxUnavailableException("The program container could not be removed.");
        }
        catch (Exception error) when (error is OperationCanceledException or Win32Exception or
                                      DecoderFallbackException or PlayerProgramException)
        {
            throw new ProgramSandboxUnavailableException("The program container could not be removed.", error);
        }
    }

    private Task<(int ExitCode, string Output)> Docker(string[] arguments, string input, CancellationToken cancellationToken) =>
        _process.Run(_settings.Executable, arguments, input, _limits.OutputBytes, cancellationToken);

    public void Dispose() => _slots.Dispose();
}
