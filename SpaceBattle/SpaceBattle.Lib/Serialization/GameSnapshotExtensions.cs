using App;
using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

internal static class GameSnapshotExtensions
{
    public static string RestoreKey(string name) => "Serialization.RestoreExtension." + name;

    public static void Restore(IGame game, JsonObject snapshot)
    {
        if (snapshot["extensions"] is not JsonObject extensions) return;
        if (game is not IManagedGame managed) throw new FormatException("Snapshot extensions require a game scope.");
        using var scope = managed.Scope.Enter();
        foreach (var extension in extensions)
            Ioc.Resolve<ICommand>(RestoreKey(extension.Key), extension.Value!).Execute();
    }
}
