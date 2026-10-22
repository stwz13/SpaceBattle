using System.Reflection;

namespace SpaceBattle.Lib;

public interface ISourceCompiler
{
    Assembly Compile(string source, IEnumerable<Assembly> references);
}
