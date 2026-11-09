using System.Collections.Concurrent;
using System.Reflection;
using SpaceBattle.Lib;
namespace SpaceBattle.Tests;

public class CompilerCacheIsolationTests
{
    private const string Source = "public sealed class CacheProbe { }";
    [Fact]
    public void Independent_Compilers_Do_Not_Share_Assemblies()
    {
        var first = new RoslynSourceCompiler().Compile(Source, []);
        var second = new RoslynSourceCompiler().Compile(Source, []);
        Assert.NotSame(first, second);
    }
    [Fact]
    public void Compilers_Can_Share_An_Explicitly_Injected_Cache()
    {
        var cache = new ConcurrentDictionary<string, Assembly>();
        var first = new RoslynSourceCompiler(cache).Compile(Source, []);
        var second = new RoslynSourceCompiler(cache).Compile(Source, []);
        Assert.Same(first, second);
    }
}
