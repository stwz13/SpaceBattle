using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ObjectAuthorizationTests
{
    private readonly AccessScopeTree _tree = new();
    private readonly ObjectAccessScopes _objects = new();
    private readonly AccessGrantStore _grants = new();
    private readonly IGameItem _ship = new Mock<IGameItem>().Object;
    private readonly IGameItem _otherShip = new Mock<IGameItem>().Object;
    private readonly AuthorizationPolicy _policy;

    public ObjectAuthorizationTests()
    {
        _tree.Add("game");
        _tree.Add("team", "game");
        _tree.Add("player1", "team");
        _tree.Add("player2", "team");
        _tree.Add("ship", "player1");
        _tree.Add("otherShip", "player2");
        _objects.Bind(_ship, "ship");
        _objects.Bind(_otherShip, "otherShip");
        _policy = new AuthorizationPolicy(new[] { new InheritedGrantRule(_tree, _objects, _grants) });
    }

    [Theory]
    [InlineData("game")]
    [InlineData("team")]
    [InlineData("player1")]
    [InlineData("ship")]
    public void Grants_Are_Inherited_From_Any_Ancestor_Or_Target(string scope)
    {
        _grants.Grant("user", "Move", scope);
        Assert.True(_policy.IsAllowed("user", "Move", _ship));
    }

    [Fact]
    public void Grants_Do_Not_Leak_To_Siblings_Ancestors_Other_Users_Or_Actions()
    {
        _grants.Grant("user", "Move", "player1");
        Assert.False(_policy.IsAllowed("user", "Move", _otherShip));
        Assert.False(_policy.IsAllowed("other", "Move", _ship));
        Assert.False(_policy.IsAllowed("user", "Shoot", _ship));
        _objects.Bind(_ship, "team");
        Assert.False(_policy.IsAllowed("user", "Move", _ship));
    }

    [Fact]
    public void Missing_Unknown_And_Unbound_Targets_Are_Denied()
    {
        _grants.Grant("user", "Move", "game");
        Assert.False(_policy.IsAllowed("user", "Move", new Mock<IGameItem>().Object));
        _objects.Bind(_ship, "missing");
        _grants.Grant("user", "Move", "missing");
        Assert.False(_policy.IsAllowed("user", "Move", _ship));
        _objects.Bind(_ship, "ship");
        _objects.Unbind(_ship);
        Assert.False(_policy.IsAllowed("user", "Move", _ship));
    }

    [Fact]
    public void Revoking_A_Grant_Takes_Effect_Immediately_And_Grant_Is_Idempotent()
    {
        _grants.Grant("user", "Move", "game");
        _grants.Grant("user", "Move", "game");
        Assert.True(_policy.IsAllowed("user", "Move", _ship));
        _grants.Revoke("user", "Move", "game");
        _grants.Revoke("user", "Move", "game");
        Assert.False(_policy.IsAllowed("user", "Move", _ship));
    }

    [Fact]
    public void Moving_A_Subtree_Updates_Descendants_And_Preserves_Direct_Grants()
    {
        _tree.Add("component", "ship");
        _objects.Bind(_ship, "component");
        _grants.Grant("old", "Move", "player1");
        _grants.Grant("next", "Move", "player2");
        _grants.Grant("direct", "Move", "component");
        _tree.Move("ship", "player2");
        Assert.False(_policy.IsAllowed("old", "Move", _ship));
        Assert.True(_policy.IsAllowed("next", "Move", _ship));
        Assert.True(_policy.IsAllowed("direct", "Move", _ship));
        Assert.Equal(new[] { "game", "team", "player2", "ship", "component" }, _tree.GetPath("component"));
        _tree.Move("ship", null);
        Assert.Equal(new[] { "ship", "component" }, _tree.GetPath("component"));
    }

    [Fact]
    public void Invalid_Tree_Mutations_Leave_The_Tree_Intact()
    {
        Assert.Throws<ArgumentException>(() => _tree.Add("ship"));
        Assert.Throws<KeyNotFoundException>(() => _tree.Add("new", "missing"));
        Assert.Throws<KeyNotFoundException>(() => _tree.Move("ship", "missing"));
        Assert.Throws<KeyNotFoundException>(() => _tree.Move("missing", "game"));
        Assert.Throws<InvalidOperationException>(() => _tree.Move("team", "ship"));
        Assert.Throws<InvalidOperationException>(() => _tree.Move("ship", "ship"));
        Assert.Equal(new[] { "game", "team", "player1", "ship" }, _tree.GetPath("ship"));
        Assert.Empty(_tree.GetPath("new"));
    }

    [Fact]
    public void Returned_Paths_Cannot_Mutate_The_Tree_And_Remain_Snapshots()
    {
        var path = _tree.GetPath("ship");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)path)[0] = "changed");
        _tree.Move("ship", "player2");
        Assert.Equal("player1", path[2]);
        Assert.Equal("player2", _tree.GetPath("ship")[2]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_New_Deny_Rule_Overrides_Grants_Regardless_Of_Order(bool denyFirst)
    {
        _grants.Grant("user", "Move", "game");
        var allow = new InheritedGrantRule(_tree, _objects, _grants);
        var deny = new Mock<IAuthorizationRule>();
        deny.Setup(r => r.Evaluate("user", "Move", _ship)).Returns(AuthorizationDecision.Deny);
        IAuthorizationRule[] rules = denyFirst ? new IAuthorizationRule[] { deny.Object, allow } : new IAuthorizationRule[] { allow, deny.Object };
        Assert.False(new AuthorizationPolicy(rules).IsAllowed("user", "Move", _ship));
    }

    [Fact]
    public void Empty_Abstaining_And_Unknown_Decisions_Deny_Access()
    {
        Assert.False(new AuthorizationPolicy(Array.Empty<IAuthorizationRule>()).IsAllowed("user", "Move", _ship));
        Assert.False(_policy.IsAllowed("user", "Move", _ship));
        var unknown = new Mock<IAuthorizationRule>();
        unknown.Setup(r => r.Evaluate("user", "Move", _ship)).Returns((AuthorizationDecision)123);
        _grants.Grant("user", "Move", "game");
        Assert.False(new AuthorizationPolicy(new IAuthorizationRule[] { new InheritedGrantRule(_tree, _objects, _grants), unknown.Object }).IsAllowed("user", "Move", _ship));
    }

    [Fact]
    public void Queued_Action_Rechecks_Ownership_Before_Composition_And_Execution()
    {
        _grants.Grant("user", "Move", "player1");
        var composed = 0;
        var action = new Mock<Lib.ICommand>();
        var factory = new AuthorizedActionFactory(_policy, (_, _) => { composed++; return action.Object; });
        var queue = new Queue<Lib.ICommand>();
        queue.Enqueue(factory.Create("user", "Move", _ship));
        Assert.Equal(0, composed);
        _tree.Move("ship", "player2");
        Assert.Throws<UnauthorizedAccessException>(() => queue.Dequeue().Execute());
        Assert.Equal(0, composed);
        action.Verify(c => c.Execute(), Times.Never);
    }

    [Fact]
    public void Reused_Commands_Recheck_Grants_And_Object_Binding_Each_Time()
    {
        var action = new Mock<Lib.ICommand>();
        var command = new AuthorizedActionFactory(_policy, (_, _) => action.Object).Create("user", "Move", _ship);
        _grants.Grant("user", "Move", "player1");
        command.Execute();
        _grants.Revoke("user", "Move", "player1");
        Assert.Throws<UnauthorizedAccessException>(command.Execute);
        _grants.Grant("user", "Move", "player1");
        _objects.Bind(_ship, "otherShip");
        Assert.Throws<UnauthorizedAccessException>(command.Execute);
        action.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void Authorized_Action_Uses_Existing_Configured_Composition()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyAuthorizedActions(_policy).Execute();
        var executed = new List<string>();
        Ioc.Resolve<App.ICommand>("IoC.Register", "AuthorizedMove.Config", (object[] _) => new[] { "Authorized.Test.First", "Authorized.Test.Second" }).Execute();
        foreach (var name in new[] { "First", "Second" })
        {
            Ioc.Resolve<App.ICommand>("IoC.Register", "Authorized.Test." + name, (object[] args) =>
            {
                Assert.Same(_ship, args[0]);
                var command = new Mock<Lib.ICommand>();
                command.Setup(c => c.Execute()).Callback(() => executed.Add(name));
                return command.Object;
            }).Execute();
        }
        var guarded = Ioc.Resolve<Lib.ICommand>(AuthorizationDependencies.Command, "user", "AuthorizedMove", _ship);
        Assert.Throws<UnauthorizedAccessException>(guarded.Execute);
        _grants.Grant("user", "AuthorizedMove", "player1");
        guarded.Execute();
        Assert.Equal(new[] { "First", "Second" }, executed);
        Assert.Same(_policy, Ioc.Resolve<IAuthorizationPolicy>(AuthorizationDependencies.Policy));
        Assert.IsType<AuthorizedActionFactory>(Ioc.Resolve<IAuthorizedActionFactory>(AuthorizationDependencies.Factory));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(AuthorizationDependencies.Command, "user", "Move"));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(AuthorizationDependencies.Command, "user", "Move", "ship"));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IAuthorizedActionFactory>(AuthorizationDependencies.Factory, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IAuthorizationPolicy>(AuthorizationDependencies.Policy, 1));
    }

    [Fact]
    public void Policy_Exceptions_Fail_Closed_And_Action_Errors_Are_Not_Swallowed()
    {
        var policy = new Mock<IAuthorizationPolicy>();
        var failure = new InvalidOperationException("Policy failed");
        policy.Setup(p => p.IsAllowed("user", "Move", _ship)).Throws(failure);
        var compositions = 0;
        var factory = new AuthorizedActionFactory(policy.Object, (_, _) =>
        {
            compositions++;
            return Mock.Of<Lib.ICommand>();
        });
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(factory.Create("user", "Move", _ship).Execute));
        Assert.Equal(0, compositions);
        _grants.Grant("user", "Move", "game");
        var action = new Mock<Lib.ICommand>();
        action.Setup(c => c.Execute()).Throws(failure);
        var command = new AuthorizedActionFactory(_policy, (_, _) => action.Object).Create("user", "Move", _ship);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(command.Execute));
    }

    [Fact]
    public void Each_Game_Scope_Can_Register_An_Independent_Policy()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyAuthorizedActions(_policy).Execute();
        _grants.Grant("user", "Move", "game");
        var childPolicy = new AuthorizationPolicy(Array.Empty<IAuthorizationRule>());
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyAuthorizedActions(childPolicy).Execute();
        Assert.Same(childPolicy, Ioc.Resolve<IAuthorizationPolicy>(AuthorizationDependencies.Policy));
        var command = Ioc.Resolve<Lib.ICommand>(AuthorizationDependencies.Command, "user", "Move", _ship);
        Assert.Throws<UnauthorizedAccessException>(command.Execute);
        Assert.True(_policy.IsAllowed("user", "Move", _ship));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Invalid_Identifiers_Are_Rejected(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => _tree.Add(value!));
        Assert.ThrowsAny<ArgumentException>(() => _grants.Grant(value!, "Move", "ship"));
        Assert.ThrowsAny<ArgumentException>(() => _grants.Grant("user", value!, "ship"));
        Assert.ThrowsAny<ArgumentException>(() => _grants.Grant("user", "Move", value!));
        Assert.ThrowsAny<ArgumentException>(() => _policy.IsAllowed(value!, "Move", _ship));
        Assert.ThrowsAny<ArgumentException>(() => new AuthorizedActionFactory(_policy, (_, _) => Mock.Of<Lib.ICommand>()).Create("user", value!, _ship));
    }
}
