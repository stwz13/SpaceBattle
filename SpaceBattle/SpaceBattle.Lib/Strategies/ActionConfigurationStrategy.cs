namespace SpaceBattle.Lib;

public static class ActionConfigurationStrategy
{
    public static Func<object[], object> Create(string path) => _ =>
        File.ReadAllLines(path)
            .Select(line => line.Split('#')[0])
            .SelectMany(line => line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToArray();
}
