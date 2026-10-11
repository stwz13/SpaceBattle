using Microsoft.CodeAnalysis.CSharp;

namespace SpaceBattle.Tests;

public class ArchitectureConstraintsTests
{
    private static string LibraryDirectory => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SpaceBattle.Lib"));

    [Fact]
    public void Subject_Code_Has_No_New_Or_Direct_IoC_Access()
    {
        foreach (var folder in new[] { "Commands", "ValueObjects", "Threads", "Receivers", "Processors", "Behaviours", "Queues" }
            .Where(folder => Directory.Exists(Path.Combine(LibraryDirectory, folder))))
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(LibraryDirectory, folder), "*.cs"))
            {
                var tokens = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantTokens().ToList();
                Assert.False(tokens.Any(t => t.RawKind == (int)SyntaxKind.NewKeyword), $"Object construction in {path}");
                Assert.False(tokens.Any(t => t.ValueText is "Ioc" or "IoC"), $"Direct IoC access in {path}");
            }
        }
    }

    [Fact]
    public void Source_Has_No_For_Or_While_Loops()
    {
        foreach (var path in Directory.EnumerateFiles(LibraryDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
        {
            var tokens = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantTokens();
            Assert.False(tokens.Any(t => t.RawKind is (int)SyntaxKind.ForKeyword or (int)SyntaxKind.WhileKeyword),
                $"Forbidden loop in {path}");
        }
    }
}
