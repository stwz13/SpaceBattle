namespace SpaceBattle.Lib;

public static class ServerDependencies
{
    public const string Instance = DependencyNamespaces.Server + ".Instance";
    public const string Start = DependencyNamespaces.Server + ".Start";
    public const string SoftStop = DependencyNamespaces.Server + ".SoftStop";
    public const string HardStop = DependencyNamespaces.Server + ".HardStop";
}
