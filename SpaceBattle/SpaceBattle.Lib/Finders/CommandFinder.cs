using System.Reflection;

namespace SpaceBattle.Lib;

public class CommandFinder : ICommandFinder
{
    public IReadOnlyList<(Type Command, Type Interface)> Find(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t.IsClass && t.IsVisible && !t.IsAbstract && !t.ContainsGenericParameters)
            .Where(t => typeof(ICommand).IsAssignableFrom(t) && t.Name.EndsWith("Command"))
            .Select(t => (Command: t, Interface: t.GetConstructors()
                .Select(c => c.GetParameters())
                .Where(p => p.Length == 1 && InterfaceInspector.IsAdaptable(p[0].ParameterType))
                .Select(p => p[0].ParameterType)
                .FirstOrDefault()))
            .Where(x => x.Interface != null)
            .Select(x => (x.Command, x.Interface!))
            .ToList();
}
