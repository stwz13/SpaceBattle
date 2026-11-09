using System.Text;

namespace SpaceBattle.Lib;

public class StrategySourceBuilder : IStrategySourceBuilder
{
    public string Build(IEnumerable<Type> interfaces, IEnumerable<(Type Command, Type Interface)> commands)
    {
        var item = CSharpTypeName.Format(typeof(IGameItem));
        var sb = new StringBuilder();

        sb.AppendLine($"    public static class {GeneratedNames.StrategiesClass}");
        sb.AppendLine("    {");

        foreach (var iface in interfaces)
        {
            sb.AppendLine($"        public static object {GeneratedNames.AdapterMethod(iface)}(object[] args)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {GeneratedNames.AdapterClass(iface)}(({item})args[0]);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        foreach (var (command, iface) in commands)
        {
            sb.AppendLine($"        public static object {GeneratedNames.CommandMethod(command)}(object[] args)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {CSharpTypeName.Format(command)}(App.Ioc.Resolve<{CSharpTypeName.Format(iface)}>(\"{GeneratedNames.AdapterDependency(iface)}\", args));");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        return sb.ToString();
    }
}
