namespace SpaceBattle.Lib;

public static class MessageDependencies
{
    public const string Receiver = DependencyNamespaces.Messages + ".Receiver";
    public const string Parser = DependencyNamespaces.Messages + ".Parser";

    public static string CommandKey(string command) => DependencyNamespaces.Messages + ".Command." + command;
}
