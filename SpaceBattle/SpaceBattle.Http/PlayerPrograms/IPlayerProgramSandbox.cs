using SpaceBattle.Lib;

namespace SpaceBattle.Http;

public sealed record ProgramContainerLaunch(string Image, string EntryPoint, IReadOnlyList<string> Arguments);

public interface IPlayerProgramSandbox
{
    /// <summary>Returns stdout after the container has terminated and been removed.</summary>
    /// <exception cref="PlayerProgramException">The program failed or exceeded an execution limit.</exception>
    /// <exception cref="ProgramSandboxUnavailableException">The container environment or cleanup failed.</exception>
    /// <exception cref="OperationCanceledException">The request was cancelled and cleanup succeeded.</exception>
    Task<string> Run(ProgramContainerLaunch launch, string source, ProgramTurn turn, CancellationToken cancellationToken);
}
