using App;
using App.Scopes;
using SpaceBattle.Lib;
using SpaceBattle.Tests.BattleExtensions;

namespace SpaceBattle.Tests;

public class BattleContextRegistrationTests
{
    public BattleContextRegistrationTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyBattleContextFactory().Execute();
    }

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static IBattleContextFactory Factory() =>
        Ioc.Resolve<IBattleContextFactory>(BattleContextDependencies.Factory);

    private static IBattleContext Context() =>
        Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context);

    private static IGameItemStorage Storage() =>
        Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage);

    [Fact]
    public void Registered_Factory_Creates_Independent_Contexts()
    {
        var first = Factory().Create("First");
        var second = Factory().Create("Second");
        first.Storage.Add("Ship", new TestGameItem());

        Assert.Equal("First", first.Id);
        Assert.Equal("Second", second.Id);
        Assert.NotSame(first, second);
        Assert.NotSame(first.Storage, second.Storage);
        Assert.Empty(second.Storage.GetSnapshot());
    }

    [Fact]
    public void Context_Registration_Binds_The_Context_And_Its_Existing_Storage()
    {
        var storage = new GameItemStorage();
        var item = new TestGameItem();
        storage.Add("Ship", item);
        var context = new BattleContext("Battle", storage);

        new RegisterIoCDependencyBattleContext(context).Execute();

        Assert.Same(context, Context());
        Assert.Same(context, Context());
        Assert.Same(storage, Storage());
        Assert.Same(Context().Storage, Storage());
        Assert.Same(item, Storage().Get("Ship"));
        Assert.True(Storage().Remove("Ship"));
        Assert.Empty(context.Storage.GetSnapshot());
    }

    [Fact]
    public void Two_Battle_Scopes_Keep_Their_Context_And_Storage_And_Preserve_Their_Parent()
    {
        var parentContext = Factory().Create("Parent");
        new RegisterIoCDependencyBattleContext(parentContext).Execute();
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        var firstScope = Ioc.Resolve<object>("IoC.Scope.Create");
        var secondScope = Ioc.Resolve<object>("IoC.Scope.Create");
        var first = Factory().Create("First");
        var second = Factory().Create("Second");
        var firstItem = new TestGameItem();
        var secondItem = new TestGameItem();

        try
        {
            SetScope(firstScope);
            new RegisterIoCDependencyBattleContext(first).Execute();
            Assert.Same(first, Context());
            Assert.Same(first.Storage, Storage());
            Storage().Add("Ship", firstItem);

            SetScope(secondScope);
            new RegisterIoCDependencyBattleContext(second).Execute();
            Assert.Same(second, Context());
            Assert.Same(second.Storage, Storage());
            Assert.Empty(Storage().GetSnapshot());
            Storage().Add("Ship", secondItem);
            Assert.Empty(Factory().Create("Third").Storage.GetSnapshot());

            SetScope(firstScope);
            Assert.Same(first, Context());
            Assert.Same(Context().Storage, Storage());
            Assert.Same(firstItem, Storage().Get("Ship"));
            Assert.True(Context().Storage.Remove("Ship"));

            SetScope(secondScope);
            Assert.Same(second, Context());
            Assert.Same(Context().Storage, Storage());
            Assert.Same(secondItem, Storage().Get("Ship"));
        }
        finally
        {
            SetScope(parent);
        }

        Assert.Same(parentContext, Context());
        Assert.Same(parentContext.Storage, Storage());
        Assert.Empty(Storage().GetSnapshot());
    }

    [Fact]
    public void Existing_Generated_Command_Uses_The_Current_Battle_Without_Regeneration()
    {
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(CaptureBattleItemCommand).Assembly,
            [new Dependency(typeof(IBattleItemSnapshot), nameof(IBattleItemSnapshot.Battle),
                BattleContextDependencies.Context),
             new Dependency(typeof(IBattleItemSnapshot), nameof(IBattleItemSnapshot.Storage),
                GameItemStorageDependencies.Storage)]).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Register", "CaptureBattle.Config",
            (object[] args) => new[] { GeneratedNames.CommandDependency(typeof(CaptureBattleItemCommand)) }).Execute();
        var item = new TestGameItem();
        item.SetProperty("ItemId", "Ship");
        var first = Factory().Create("First");
        var second = Factory().Create("Second");
        var firstShip = new TestGameItem();
        var secondShip = new TestGameItem();
        first.Storage.Add("Ship", firstShip);
        second.Storage.Add("Ship", secondShip);
        var action = Ioc.Resolve<Lib.ICommand>("Commands.Action", "CaptureBattle", item);
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        var firstScope = Ioc.Resolve<object>("IoC.Scope.Create");
        var secondScope = Ioc.Resolve<object>("IoC.Scope.Create");

        try
        {
            SetScope(firstScope);
            new RegisterIoCDependencyBattleContext(first).Execute();
            action.Execute();
            Assert.Equal("First", item.GetProperty("BattleId"));
            Assert.Same(firstShip, item.GetProperty("SelectedItem"));

            SetScope(secondScope);
            new RegisterIoCDependencyBattleContext(second).Execute();
            action.Execute();
            Assert.Equal("Second", item.GetProperty("BattleId"));
            Assert.Same(secondShip, item.GetProperty("SelectedItem"));

            SetScope(firstScope);
            action.Execute();
            Assert.Equal("First", item.GetProperty("BattleId"));
            Assert.Same(firstShip, item.GetProperty("SelectedItem"));
            Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Battle"));
            Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Storage"));
        }
        finally
        {
            SetScope(parent);
        }
    }

    [Fact]
    public void Null_Context_Is_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyBattleContext(null!));
    }
}
