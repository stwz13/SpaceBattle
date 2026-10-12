using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SpaceBattle.Tests;

public class ArchitectureConstraintsTests
{
    private static string LibraryDirectory => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SpaceBattle.Lib"));

    [Fact]
    public void Subject_Code_Constructs_Only_Allowed_Values_And_Has_No_Direct_IoC_Access()
    {
        foreach (var folder in new[] { "Commands", "ValueObjects", "Threads", "Receivers", "Processors", "Behaviours", "Queues" }
            .Where(folder => Directory.Exists(Path.Combine(LibraryDirectory, folder))))
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(LibraryDirectory, folder), "*.cs"))
            {
                var tokens = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantTokens().ToList();
                foreach (var token in tokens.Where(t => t.RawKind == (int)SyntaxKind.NewKeyword))
                {
                    var type = (token.Parent as ObjectCreationExpressionSyntax)?.Type.ToString();
                    var allowed = type is "Vector" or "Angle" or "SpaceBattle.Lib.Vector" or "SpaceBattle.Lib.Angle"
                        || (folder == "ValueObjects" && Path.GetFileName(path) == "Vector.cs"
                            && type == "ArgumentException");
                    Assert.True(allowed, $"Forbidden object construction in {path}: {type}");
                }
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
