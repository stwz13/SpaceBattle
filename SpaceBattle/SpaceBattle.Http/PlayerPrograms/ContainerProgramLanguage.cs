using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal sealed record ContainerProgramLanguage(string Name, IPlayerProgramSandbox Sandbox,
    ProgramContainerLaunch Launch) : IPlayerProgramLanguage
{
    public Task<string> Run(string source, ProgramTurn turn, CancellationToken cancellationToken) =>
        Sandbox.Run(Launch, source, turn, cancellationToken);

    public static IPlayerProgramLanguage Create(ContainerLanguageSettings settings, IPlayerProgramSandbox sandbox)
    {
        var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, settings.Bootstrap));
        var launch = new ProgramContainerLaunch(settings.Image, settings.EntryPoint, [.. settings.Arguments, script]);
        return new ContainerProgramLanguage(settings.Name, sandbox, launch);
    }
}
