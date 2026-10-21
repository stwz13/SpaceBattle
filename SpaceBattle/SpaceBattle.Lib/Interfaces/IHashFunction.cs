namespace SpaceBattle.Lib;

public interface IHashFunction
{
    ulong Hash(string key);
}
