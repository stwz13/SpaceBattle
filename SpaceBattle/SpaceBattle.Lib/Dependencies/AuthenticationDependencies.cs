namespace SpaceBattle.Lib;

public static class AuthenticationDependencies
{
    public static string AccessRule(string name) => "Auth.AccessRule." + name;
    public const string Validator = "Auth.Validator";
    public const string Authenticate = "Messages.Authenticate";
    public const string CommandRules = "Auth.CommandRules";
}
