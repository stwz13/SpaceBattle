using System.Reflection;
using System.Runtime.Loader;
using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal sealed class CommandPluginLoadContext(string path) : AssemblyLoadContext
{
    private static readonly Assembly Contract = typeof(ICommandPlugin).Assembly;
    private readonly AssemblyDependencyResolver _resolver = new(path);

    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name == Contract.GetName().Name)
            return Contract;

        var dependency = _resolver.ResolveAssemblyToPath(name);
        return dependency is null ? null : LoadFromAssemblyPath(dependency);
    }

    protected override nint LoadUnmanagedDll(string name)
    {
        var dependency = _resolver.ResolveUnmanagedDllToPath(name);
        return dependency is null ? 0 : LoadUnmanagedDllFromPath(dependency);
    }
}
