using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class RoutingRegistrationTests
{
    public RoutingRegistrationTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyRouting(64).Execute();
    }

    private static IConsistentHashRing<IServerThread> Ring() =>
        Ioc.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring);

    private static IServerThread Route(string gameId) => Ioc.Resolve<IServerThread>(RoutingDependencies.Thread, gameId);

    [Fact]
    public void Ring_Is_One_Shared_Object()
    {
        Assert.Same(Ring(), Ring());
    }

    [Fact]
    public void Game_Is_Routed_To_A_Thread_On_The_Ring_And_Always_To_The_Same_One()
    {
        var first = new StubServerThread("first");
        var second = new StubServerThread("second");
        Ring().Add("first", first);
        Ring().Add("second", second);

        var routes = Enumerable.Range(0, 200).Select(index => Route($"Battle-{index}")).ToArray();

        Assert.All(routes, route => Assert.True(ReferenceEquals(route, first) || ReferenceEquals(route, second)));
        Assert.Contains(first, routes);
        Assert.Contains(second, routes);
        Assert.All(Enumerable.Range(0, 200), index => Assert.Same(Route($"Battle-{index}"), Route($"Battle-{index}")));
    }

    [Fact]
    public void Routing_Follows_The_Ring_When_Threads_Come_And_Go()
    {
        var only = new StubServerThread("only");
        Ring().Add("only", only);
        Assert.Same(only, Route("Battle"));

        var other = new StubServerThread("other");
        Ring().Add("other", other);
        Ring().Remove("only");

        Assert.Same(other, Route("Battle"));
    }

    [Fact]
    public void Routing_Without_Threads_Fails()
    {
        Assert.Throws<InvalidOperationException>(() => Route("Battle"));
    }

    [Fact]
    public void Routing_Needs_Exactly_One_Game_Identifier()
    {
        Ring().Add("only", new StubServerThread("only"));

        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IServerThread>(RoutingDependencies.Thread));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IServerThread>(RoutingDependencies.Thread, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IServerThread>(RoutingDependencies.Thread, "A", "B"));
    }

    [Fact]
    public void Another_Scope_Can_Bring_Its_Own_Ring()
    {
        var shared = new StubServerThread("shared");
        Ring().Add("shared", shared);
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        try
        {
            new RegisterIoCDependencyRouting(8).Execute();
            var own = new StubServerThread("own");
            Ring().Add("own", own);

            Assert.Same(own, Route("Battle"));
        }
        finally
        {
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", parent).Execute();
        }

        Assert.Same(shared, Route("Battle"));
    }

    [Fact]
    public void A_Given_Hash_Function_Is_Used_For_Routing()
    {
        var hash = new FakeHashFunction(new Dictionary<string, ulong>
        {
            ["low#0"] = 100,
            ["high#0"] = 200,
            ["Battle-1"] = 150,
            ["Battle-2"] = 250,
        });
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        try
        {
            new RegisterIoCDependencyRouting(1, hash).Execute();
            var low = new StubServerThread("low");
            var high = new StubServerThread("high");
            Ring().Add("low", low);
            Ring().Add("high", high);

            Assert.Same(high, Route("Battle-1"));
            Assert.Same(low, Route("Battle-2"));
        }
        finally
        {
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", parent).Execute();
        }
    }

    [Fact]
    public void Registration_Rejects_Invalid_Arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegisterIoCDependencyRouting(0));
        Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyRouting(1, null!));
    }

    [Fact]
    public void Default_Hash_Function_Is_The_Fast_One()
    {
        var first = new StubServerThread("first");
        var second = new StubServerThread("second");
        Ring().Add("first", first);
        Ring().Add("second", second);
        var expected = new ConsistentHashRing<IServerThread>(new FnvHashFunction(), 64);
        expected.Add("first", first);
        expected.Add("second", second);

        Assert.All(Enumerable.Range(0, 200), index => Assert.Same(expected.Find($"Battle-{index}"), Route($"Battle-{index}")));
    }
}
