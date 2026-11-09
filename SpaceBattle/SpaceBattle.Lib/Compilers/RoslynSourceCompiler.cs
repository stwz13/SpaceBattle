using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SpaceBattle.Lib;

public class RoslynSourceCompiler : ISourceCompiler
{
    private readonly ConcurrentDictionary<string, Assembly> _compiled;

    public RoslynSourceCompiler() : this(new ConcurrentDictionary<string, Assembly>()) { }

    public RoslynSourceCompiler(ConcurrentDictionary<string, Assembly> compiled)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        _compiled = compiled;
    }

    public Assembly Compile(string source, IEnumerable<Assembly> references)
    {
        return _compiled.GetOrAdd(source, s =>
        {
            var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            var paths = references.Select(a => a.Location)
                .Concat(trusted)
                .Where(p => !string.IsNullOrEmpty(p))
                .GroupBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());

            var compilation = CSharpCompilation.Create(
                GeneratedNames.Namespace + "_" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(s) },
                paths.Select(p => MetadataReference.CreateFromFile(p)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);

            if (!result.Success)
            {
                var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
                throw new InvalidOperationException("Generated code does not compile:\n" + string.Join("\n", errors));
            }

            return Assembly.Load(stream.ToArray());
        });
    }
}
