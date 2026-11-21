namespace SpaceBattle.Lib;

public static class GameRuleParameterNames
{
    internal const string ObjectPrefix = "objects.";
    public static string ForObject(string kind) => ForTarget(ObjectPrefix, kind);
    public static string ForCommand(string command) => ForTarget("commands.", command);

    private static string ForTarget(string prefix, string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        return prefix + target;
    }
}
