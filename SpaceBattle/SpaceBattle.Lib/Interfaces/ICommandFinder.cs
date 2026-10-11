using System.Reflection;

namespace SpaceBattle.Lib;

public interface ICommandFinder
{
    IReadOnlyList<(Type Command, Type Interface)> Find(Assembly assembly);
}
