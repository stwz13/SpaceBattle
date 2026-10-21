namespace SpaceBattle.Lib;

public static class ServerThreadDependencies
{
    public const string Create = DependencyNamespaces.ServerThreads + ".Create";
    public const string ExceptionHandler = DependencyNamespaces.ServerThreads + ".ExceptionHandler";
    public const string HardStop = DependencyNamespaces.Commands + ".HardStop";
    public const string SoftStop = DependencyNamespaces.Commands + ".SoftStop";
}
