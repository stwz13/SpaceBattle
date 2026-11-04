namespace SpaceBattle.Lib;

public interface IAccessAreaTree
{
    IReadOnlyList<string> GetPath(string area);
}
