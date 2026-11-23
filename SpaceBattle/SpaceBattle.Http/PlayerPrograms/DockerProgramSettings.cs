namespace SpaceBattle.Http;

public sealed record DockerProgramSettings(string Executable, int ParallelRuns, TimeSpan ExecutionTimeout,
    TimeSpan CleanupTimeout, TimeSpan StreamDrainTimeout, int MemoryMiB, int TmpfsMiB, int ProcessLimit, decimal Cpus,
    int UserId, int GroupId);

internal sealed record ContainerLanguageSettings(string Name, string Image, string EntryPoint);
