using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal static class CommandPluginLoader
{
    public static IReadOnlyDictionary<string, ICommandPlugin> Load(IEnumerable<string> paths, ServerOptions options)
    {
        var plugins = new Dictionary<string, ICommandPlugin>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var fullPath = Path.GetFullPath(path, AppContext.BaseDirectory);
            var assembly = new CommandPluginLoadContext(fullPath).LoadFromAssemblyPath(fullPath);
            var types = assembly.GetExportedTypes().Where(type => type.IsClass && !type.IsAbstract
                && !type.ContainsGenericParameters && typeof(ICommandPlugin).IsAssignableFrom(type)).ToArray();
            if (types.Length == 0)
                throw new InvalidOperationException($"Plugin '{fullPath}' exports no command plugins.");

            foreach (var type in types)
            {
                var plugin = (ICommandPlugin)Activator.CreateInstance(type)!;
                var name = plugin.CommandName;
                if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name == ItemDependencies.CreateCommandKey)
                    throw new InvalidOperationException($"Invalid or reserved plugin command name '{name}' in '{fullPath}'.");
                if (!options.CommandTargets.TryGetValue(name, out var target) || string.IsNullOrWhiteSpace(target))
                    throw new InvalidOperationException($"Configure Server:CommandTargets:{name} before loading '{fullPath}'.");
                if (!plugins.TryAdd(name, plugin))
                    throw new InvalidOperationException($"Plugin command '{name}' is registered more than once.");
            }
        }

        return plugins;
    }
}
