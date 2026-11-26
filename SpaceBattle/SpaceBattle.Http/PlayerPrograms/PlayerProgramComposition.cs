using Microsoft.Extensions.DependencyInjection.Extensions;
using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal static class PlayerProgramComposition
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("PlayerPrograms");
        services.TryAddSingleton<IPlayerProgramLimits>(_ =>
        {
            var limits = Required<PlayerProgramLimits>(settings, "Limits");
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limits.SourceBytes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limits.OutputBytes);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limits.JsonDepth);
            ArgumentOutOfRangeException.ThrowIfEqual(limits.SourceBytes, int.MaxValue);
            ArgumentOutOfRangeException.ThrowIfEqual(limits.OutputBytes, int.MaxValue);
            return limits;
        });
        services.TryAddSingleton(_ => Required<DockerProgramSettings>(settings, "Docker"));
        foreach (var key in settings.GetSection("Languages").Get<string[]>() ?? [])
        {
            services.AddSingleton<IPlayerProgramLanguage>(provider => ContainerProgramLanguage.Create(
                Required<ContainerLanguageSettings>(settings, key),
                provider.GetRequiredService<IPlayerProgramSandbox>()));
        }
        services.TryAddSingleton<IPlayerProgramStore, MemoryPlayerProgramStore>();
        services.TryAddSingleton<IPlayerProgramGame, PlayerProgramGame>();
        services.TryAddSingleton<IProgramProcess>(provider =>
            new ProgramProcess(provider.GetRequiredService<DockerProgramSettings>().StreamDrainTimeout));
        services.TryAddSingleton<IPlayerProgramSandbox, DockerPlayerProgramSandbox>();
        services.TryAddSingleton<PlayerProgramService>();
    }

    private static T Required<T>(IConfiguration configuration, string key) where T : class =>
        configuration.GetRequiredSection(key).Get<T>() ?? throw new InvalidOperationException($"Missing player program settings '{key}'.");
}
