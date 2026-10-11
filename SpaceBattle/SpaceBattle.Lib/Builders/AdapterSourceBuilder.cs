using System.Text;

namespace SpaceBattle.Lib;

public class AdapterSourceBuilder : IAdapterSourceBuilder
{
    public string Build(Type iface)
    {
        var item = CSharpTypeName.Format(typeof(IGameItem));
        var command = CSharpTypeName.Format(typeof(ICommand));
        var className = GeneratedNames.AdapterClass(iface);
        var sb = new StringBuilder();

        sb.AppendLine($"    public class {className} : {CSharpTypeName.Format(iface)}");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {item} _item;");
        sb.AppendLine();
        sb.AppendLine($"        public {className}({item} item)");
        sb.AppendLine("        {");
        sb.AppendLine("            _item = item;");
        sb.AppendLine("        }");

        foreach (var p in InterfaceInspector.Properties(iface))
        {
            var type = CSharpTypeName.Format(p.PropertyType);
            var owner = CSharpTypeName.Format(p.DeclaringType!);
            var key = GeneratedNames.PropertyDependency(iface, p);

            sb.AppendLine();
            sb.AppendLine($"        {type} {owner}.@{p.Name}");
            sb.AppendLine("        {");
            if (p.CanRead)
            {
                sb.AppendLine($"            get {{ return App.Ioc.Resolve<{type}>(\"{key}\", _item); }}");
            }
            if (p.CanWrite)
            {
                sb.AppendLine($"            set {{ App.Ioc.Resolve<{command}>(\"{key}.Set\", _item, value).Execute(); }}");
            }
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        return sb.ToString();
    }
}
