using System.Text.Json;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameItemCreationTests
{
    public GameItemCreationTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyBattleContextFactory().Execute();
        new RegisterIoCDependencyGameItemFactory().Execute();
        new RegisterIoCDependencyGameQuantum(TimeSpan.FromHours(1)).Execute();
        new RegisterIoCDependencyGame().Execute();
        new RegisterIoCDependencyGameSnapshot().Execute();
        new RegisterIoCDependencyGameItems().Execute();
        new RegisterIoCDependencyItemKind("Ship", ["Position", "Fuel"]).Execute();
    }

    private static readonly KeyValuePair<string, object>[] ShipProperties =
        [KeyValuePair.Create("Position", (object)new Vector(1, 2)), KeyValuePair.Create("Fuel", (object)10)];

    private static IGame Create(string id) => Ioc.Resolve<IGame>(GameDependencies.Create, id);

    private static void Run(IGame game, Action action)
    {
        game.Post(new DelegateStep(action));
        game.Execute();
    }

    private static Message CreateMessage(string game, object args) =>
        new(game, ItemDependencies.CreateCommandKey, JsonSerializer.SerializeToElement(args));

    [Fact]
    public void Created_Item_Has_Id_Kind_And_Properties_And_Is_In_The_Storage()
    {
        var game = Create("Battle");

        Run(game, () =>
        {
            var item = Ioc.Resolve<IGameItem>(ItemDependencies.Create, "Ship", "s1", ShipProperties.AsEnumerable());

            Assert.Equal("s1", item.GetProperty("Id"));
            Assert.Equal("Ship", item.GetProperty("Kind"));
            Assert.Equal(new Vector(1, 2), item.GetProperty("Position"));
            Assert.Same(item, Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Get("s1"));
        });
    }

    [Fact]
    public void Creating_An_Item_With_A_Taken_Id_Or_Reserved_Property_Fails()
    {
        var game = Create("Battle");
        Run(game, () => Ioc.Resolve<IGameItem>(ItemDependencies.Create, "Ship", "s1", ShipProperties.AsEnumerable()));

        Run(game, () =>
        {
            Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameItem>(ItemDependencies.Create, "Ship", "s1", ShipProperties.AsEnumerable()));
            var withId = Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameItem>(ItemDependencies.Create, "Ship", "s2",
                new[] { KeyValuePair.Create("Id", (object)"x") }.AsEnumerable()));
            var withKind = Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameItem>(ItemDependencies.Create, "Ship", "s3",
                new[] { KeyValuePair.Create("Kind", (object)"x") }.AsEnumerable()));
            Assert.Contains("set by the creation", withId.Message);
            Assert.Contains("set by the creation", withKind.Message);
            Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameItem>(ItemDependencies.Create, "Ship"));
        });
    }

    [Fact]
    public void Message_Creates_An_Item_From_Tagged_Values_And_Snapshot_Uses_The_Kind_Scheme()
    {
        var game = Create("Battle");
        var interpreter = new IoCMessageInterpreter();
        game.Post(interpreter.Interpret(CreateMessage("Battle", new
        {
            kind = "Ship",
            id = "s1",
            properties = new
            {
                Position = new { type = "vector", value = new[] { 3, 4 } },
                Fuel = new { type = "int", value = 7 },
            },
        })));
        game.Execute();

        string? json = null;
        game.Post(Ioc.Resolve<Lib.ICommand>(SerializationDependencies.Snapshot, game, (Action<string>)(text => json = text)));
        game.Execute();

        var item = JsonDocument.Parse(json!).RootElement.GetProperty("items")[0];
        Assert.Equal("s1", item.GetProperty("id").GetString());
        Assert.Equal("Ship", item.GetProperty("kind").GetString());
        Assert.Equal(["Position", "Fuel"], item.GetProperty("properties").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Message_Without_Required_Fields_Is_Rejected()
    {
        var game = Create("Battle");
        var interpreter = new IoCMessageInterpreter();

        game.Post(interpreter.Interpret(CreateMessage("Battle", new { id = "s1" })));
        Assert.Throws<MessageFormatException>(game.Execute);
        game.Post(interpreter.Interpret(CreateMessage("Battle", new { kind = "Ship" })));
        Assert.Throws<MessageFormatException>(game.Execute);
        game.Post(interpreter.Interpret(new Message("Battle", ItemDependencies.CreateCommandKey, JsonSerializer.SerializeToElement(5))));
        Assert.ThrowsAny<Exception>(game.Execute);
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(MessageDependencies.CommandKey("CreateItem")));
    }

    [Fact]
    public void Constructors_And_Schema_Check_Their_Arguments()
    {
        var creator = new ItemCreator(new GameItemFactory(), () => null!);
        var codec = new JsonValueCodec();

        Assert.Throws<ArgumentException>(() => new CreateItemCommand(creator, " ", "s1", ShipProperties));
        Assert.Throws<ArgumentException>(() => new CreateItemCommand(creator, "Ship", " ", ShipProperties));
        Assert.Throws<ArgumentNullException>(() => new CreateItemCommand(creator, "Ship", "s1", null!));
        Assert.Throws<ArgumentNullException>(() => new CreateItemCommand(null!, "Ship", "s1", ShipProperties));
        Assert.Throws<ArgumentNullException>(() => new CreateItemMessageCommand(null!, codec, creator));
        Assert.Throws<ArgumentNullException>(() => new CreateItemMessageCommand(CreateMessage("g", 1), null!, creator));
        Assert.Throws<ArgumentNullException>(() => new CreateItemMessageCommand(CreateMessage("g", 1), codec, null!));
        Assert.Throws<ArgumentNullException>(() => new ItemCreator(null!, () => null!));
        Assert.Throws<ArgumentNullException>(() => new ItemCreator(new GameItemFactory(), null!));
        Assert.Throws<ArgumentException>(() => creator.Create(" ", "s1", ShipProperties));
        Assert.Throws<ArgumentException>(() => creator.Create("Ship", " ", ShipProperties));
        Assert.Throws<ArgumentNullException>(() => creator.Create("Ship", "s1", null!));
        Assert.Throws<ArgumentException>(() => new RegisterIoCDependencyItemKind(" ", []));
        Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyItemKind("Ship", null!));
        Assert.Equal(["Position", "Fuel"], Ioc.Resolve<IEnumerable<string>>(SerializationDependencies.Schema("Ship")));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IEnumerable<string>>(SerializationDependencies.Schema("Ship"), 1));
    }
}
