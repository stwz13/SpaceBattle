namespace SpaceBattle.Lib;

public interface IWorldPhaseConfigurationLoader
{
    IReadOnlyList<WorldPhaseDescription> Load(string path);
}
