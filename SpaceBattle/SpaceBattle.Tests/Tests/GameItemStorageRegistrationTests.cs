using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameItemStorageRegistrationTests
{
    public GameItemStorageRegistrationTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
    }

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static IGameItemStorage Storage() =>
        Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage);

    [Fact]
    public void Registration_Returns_The_Provided_Instance_On_Every_Resolution()
    {
        var storage = new Mock<IGameItemStorage>(MockBehavior.Strict);

        new RegisterIoCDependencyGameItemStorage(storage.Object).Execute();

        Assert.Same(storage.Object, Storage());
        Assert.Same(storage.Object, Storage());
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public void Two_Child_Scopes_Can_Bind_Independent_Storages_Without_Changing_Their_Parent()
    {
        var parentStorage = new GameItemStorage();
        new RegisterIoCDependencyGameItemStorage(parentStorage).Execute();
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        var firstScope = Ioc.Resolve<object>("IoC.Scope.Create");
        var secondScope = Ioc.Resolve<object>("IoC.Scope.Create");
        var firstStorage = new GameItemStorage();
        var secondStorage = new GameItemStorage();
        var firstItem = new TestGameItem();
        var secondItem = new TestGameItem();

        try
        {
            SetScope(firstScope);
            new RegisterIoCDependencyGameItemStorage(firstStorage).Execute();
            Storage().Add("Ship", firstItem);
            Assert.Same(firstStorage, Storage());
            Assert.Same(firstStorage, Storage());

            SetScope(secondScope);
            new RegisterIoCDependencyGameItemStorage(secondStorage).Execute();
            Assert.Same(secondStorage, Storage());
            Assert.Empty(Storage().GetSnapshot());
            Storage().Add("Ship", secondItem);
            Assert.Same(secondStorage, Storage());
            Assert.Same(secondItem, Storage().Get("Ship"));

            SetScope(firstScope);
            Assert.Same(firstStorage, Storage());
            Assert.Same(firstItem, Storage().Get("Ship"));
            Assert.True(Storage().Remove("Ship"));

            SetScope(secondScope);
            Assert.Same(secondItem, Storage().Get("Ship"));
        }
        finally
        {
            SetScope(parent);
        }

        Assert.Same(parentStorage, Storage());
        Assert.Empty(Storage().GetSnapshot());
    }

    [Fact]
    public void Null_Storage_Is_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyGameItemStorage(null!));
    }
}
