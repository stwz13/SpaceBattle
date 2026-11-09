using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SpaceBattle.Tests;

public class ServerRuntimeArchitectureTests
{
    [Fact]
    public void Runtime_Modules_Only_Construct_Exceptions_And_Do_Not_Resolve_IoC()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SpaceBattle.Lib"));
        foreach (var folder in new[] { "Runtime", "Authentication", "Scheduling" })
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs"))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            Assert.DoesNotContain(tree.DescendantTokens(), token => token.ValueText is "Ioc" or "IoC");
            foreach (var creation in tree.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                Assert.EndsWith("Exception", creation.Type.ToString());
            Assert.Empty(tree.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>());
        }
    }
}
