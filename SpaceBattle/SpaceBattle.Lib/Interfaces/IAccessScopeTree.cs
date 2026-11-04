namespace SpaceBattle.Lib;

public interface IAccessScopeTree
{
    IReadOnlyList<string> GetPath(string scope);
}
