using System.Text.Json;
using System.Text.Json.Nodes;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameSnapshotTests
{
    private readonly List<string> _echoed = [];

    public GameSnapshotTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyBattleContextFactory().Execute();
        new RegisterIoCDependencyGameItemFactory().Execute();
        new RegisterIoCDependencyGameQuantum(TimeSpan.FromHours(1)).Execute();
        new RegisterIoCDependencyGame().Execute();
        new RegisterIoCDependencyGameSnapshot().Execute();
        Register(SerializationDependencies.Schema("Ship"), _ => new[] { "Position", "Angle", "Fuel", "Name" }.AsEnumerable());
        Register(MessageDependencies.CommandKey("Echo"), args => new DelegateStep(() =>
            _echoed.Add(((IMessage)args[0]).Args.GetProperty("text").GetString()!)));
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static IGame Create(string id) => Ioc.Resolve<IGame>(GameDependencies.Create, id);

    private static string Snapshot(IGame game)
    {
        string? json = null;
        game.Post(Ioc.Resolve<Lib.ICommand>(SerializationDependencies.Snapshot, game, (Action<string>)(text => json = text)));
        game.Execute();
        return json!;
    }

    private static Message Echo(string game, string text) =>
        new(game, "Echo", JsonSerializer.SerializeToElement(new { text }));

    private static IGameItem Ship(string id) => Ioc.Resolve<IGameItemFactory>(GameItemFactoryDependencies.Factory).Create(
    [
        KeyValuePair.Create("Id", (object)id),
        KeyValuePair.Create("Kind", (object)"Ship"),
        KeyValuePair.Create("Position", (object)new Vector(1, -2)),
        KeyValuePair.Create("Angle", (object)new Angle(3)),
        KeyValuePair.Create("Fuel", (object)42L),
        KeyValuePair.Create("Name", (object)"Дракон"),
        KeyValuePair.Create("Secret", (object)"not in schema"),
    ]);

    private static void Fill(IGame game, params string[] ids) => game.Post(new DelegateStep(() =>
        ids.ToList().ForEach(id => Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Add(id, Ship(id)))));

    [Fact]
    public void Snapshot_Contains_Id_Kind_And_Only_Scheme_Properties_Of_Every_Item()
    {
        var game = Create("Battle");
        Fill(game, "s1");
        game.Execute();

        var root = JsonNode.Parse(Snapshot(game))!;

        Assert.Equal("Battle", root["gameId"]!.GetValue<string>());
        var item = Assert.Single(root["items"]!.AsArray())!;
        Assert.Equal("s1", item["id"]!.GetValue<string>());
        Assert.Equal("Ship", item["kind"]!.GetValue<string>());
        Assert.Equal(["Position", "Angle", "Fuel", "Name"], item["properties"]!.AsObject().Select(p => p.Key));
        Assert.Empty(root["commands"]!.AsArray());
    }

    [Fact]
    public void Restore_Recreates_Items_With_Equal_Values_In_A_New_Game()
    {
        var game = Create("Battle");
        Fill(game, "s1", "s2");
        game.Execute();

        var restored = Ioc.Resolve<IGame>(SerializationDependencies.Restore, Snapshot(game));
        restored.Execute();
        string? seen = null;
        restored.Post(new DelegateStep(() =>
        {
            var storage = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage;
            var ship = storage.Get("s2");
            seen = string.Join("|", storage.GetSnapshot().Count, ship.GetProperty("Id"), ship.GetProperty("Kind"),
                string.Join(",", ((Vector)ship.GetProperty("Position")).Coordinates), ((Angle)ship.GetProperty("Angle")).Numerator,
                ship.GetProperty("Fuel"), ship.GetProperty("Name"));
        }));
        restored.Execute();

        Assert.Equal("Battle", restored.Id);
        Assert.Equal("2|s2|Ship|1,-2|3|42|Дракон", seen);
    }

    [Fact]
    public void Pending_Messages_Are_Saved_In_Order_And_Run_After_Restore()
    {
        var game = Create("Battle");
        var interpreter = new IoCMessageInterpreter();
        var json = string.Empty;
        game.Post(Ioc.Resolve<Lib.ICommand>(SerializationDependencies.Snapshot, game, (Action<string>)(text => json = text)));
        game.Post(new InterpretMessageCommand(interpreter, Echo("Battle", "first")));
        game.Post(new InterpretMessageCommand(interpreter, Echo("Battle", "second")));
        game.Execute();
        _echoed.Clear();

        var restored = Ioc.Resolve<IGame>(SerializationDependencies.Restore, json);
        Assert.Equal(3, restored.Pending.Count);
        restored.Execute();

        Assert.Equal(["first", "second"], _echoed);
    }

    [Fact]
    public void Snapshot_Of_A_Game_With_A_Foreign_Command_In_The_Queue_Fails()
    {
        var game = Create("Battle");
        var json = string.Empty;
        game.Post(Ioc.Resolve<Lib.ICommand>(SerializationDependencies.Snapshot, game, (Action<string>)(text => json = text)));
        game.Post(new DelegateStep(() => { }));

        var error = Assert.Throws<InvalidOperationException>(game.Execute);

        Assert.Contains("DelegateStep", error.Message);
        Assert.Empty(json);
    }

    [Fact]
    public void Snapshot_Fails_For_An_Item_With_An_Unsupported_Value()
    {
        var game = Create("Battle");
        game.Post(new DelegateStep(() => Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Add("s1",
            Ioc.Resolve<IGameItemFactory>(GameItemFactoryDependencies.Factory).Create(
            [
                KeyValuePair.Create("Id", (object)"s1"),
                KeyValuePair.Create("Kind", (object)"Ship"),
                KeyValuePair.Create("Position", (object)new object()),
                KeyValuePair.Create("Angle", (object)new Angle(1)),
                KeyValuePair.Create("Fuel", (object)1),
                KeyValuePair.Create("Name", (object)"x"),
            ]))));
        game.Execute();

        Assert.Throws<NotSupportedException>(() => Snapshot(game));
    }

    [Fact]
    public void Registrations_Check_Their_Arguments()
    {
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IValueCodec>(SerializationDependencies.Codec, "x"));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(SerializationDependencies.Snapshot, "x"));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGame>(SerializationDependencies.Restore));
    }

    [Fact]
    public void Codec_Round_Trips_All_Supported_Values_And_Rejects_Unknown_Data()
    {
        var codec = Ioc.Resolve<IValueCodec>(SerializationDependencies.Codec);
        object[] values = [7, 8L, 1.5, true, "text", new Angle(5), new Vector(3, 4, 5)];

        Assert.Equal(values, values.Select(value => codec.Decode(codec.Encode(value))));
        Assert.Throws<FormatException>(() => codec.Decode(JsonNode.Parse("""{"type":"alien","value":1}""")!));
        Assert.Throws<FormatException>(() => codec.Decode(JsonNode.Parse("""{"type":"int"}""")!));
        Assert.Throws<FormatException>(() => codec.Decode(JsonNode.Parse("""{"value":1}""")!));
        Assert.Throws<NotSupportedException>(() => codec.Encode(new object()));
        Assert.Throws<ArgumentNullException>(() => codec.Encode(null!));
        Assert.Throws<ArgumentNullException>(() => codec.Decode(null!));
    }

    [Fact]
    public void A_Value_Type_Registered_Outside_Is_Saved_And_Restored()
    {
        new RegisterIoCDependencyValueType<Guid>("guid", value => JsonValue.Create(value.ToString()), node => Guid.Parse(node.GetValue<string>())).Execute();
        var codec = Ioc.Resolve<IValueCodec>(SerializationDependencies.Codec);
        var id = Guid.NewGuid();

        var json = codec.Encode(id);

        Assert.Equal("guid", json["type"]!.GetValue<string>());
        Assert.Equal(id, codec.Decode(json));
    }

    [Fact]
    public void Registering_The_Same_Type_Or_Tag_Twice_Fails_And_Keeps_The_Old_Registration()
    {
        var codec = Ioc.Resolve<IValueCodec>(SerializationDependencies.Codec);
        var registry = Ioc.Resolve<IValueCodecRegistry>(SerializationDependencies.CodecRegistry);

        Assert.Throws<InvalidOperationException>(() => registry.Register<int>("number", value => JsonValue.Create(value), node => node.GetValue<int>()));
        Assert.Throws<InvalidOperationException>(() => registry.Register<short>("int", value => JsonValue.Create(value), node => node.GetValue<short>()));
        Assert.Throws<NotSupportedException>(() => codec.Encode((short)1));
        Assert.Equal(5, codec.Decode(codec.Encode(5)));
        Assert.Throws<ArgumentException>(() => registry.Register<Guid>(" ", _ => JsonValue.Create(1), _ => Guid.Empty));
        Assert.Throws<ArgumentNullException>(() => registry.Register<Guid>("g", null!, _ => Guid.Empty));
        Assert.Throws<ArgumentNullException>(() => registry.Register<Guid>("g", _ => JsonValue.Create(1), null!));
    }

    private sealed class Ping : Lib.ICommand
    {
        public static int Executed;

        public void Execute() => Executed++;
    }

    private sealed class PingSerializer : ICommandSerializer
    {
        public string Tag => "ping";

        public bool Handles(Lib.ICommand command) => command is Ping;

        public JsonObject Write(Lib.ICommand command) => new();

        public Lib.ICommand Read(JsonObject node) => new Ping();
    }

    [Fact]
    public void A_Command_Serializer_Registered_Outside_Lets_Other_Commands_Be_Saved_And_Restored()
    {
        Ioc.Resolve<ICollection<ICommandSerializer>>(SerializationDependencies.CommandSerializers).Add(new PingSerializer());
        Ping.Executed = 0;
        var game = Create("Battle");
        var json = string.Empty;
        game.Post(Ioc.Resolve<Lib.ICommand>(SerializationDependencies.Snapshot, game, (Action<string>)(text => json = text)));
        game.Post(new Ping());
        game.Execute();
        Ping.Executed = 0;

        Ioc.Resolve<IGame>(SerializationDependencies.Restore, json).Execute();

        Assert.Equal(1, Ping.Executed);
    }

    [Fact]
    public void Command_Serializer_Rejects_Unknown_Tags_And_Bad_Arguments()
    {
        var serializer = new CommandJsonSerializer([new MessageCommandSerializer(new IoCMessageInterpreter())]);
        var message = new MessageCommandSerializer(new IoCMessageInterpreter());

        Assert.Throws<FormatException>(() => serializer.Read(new JsonObject { ["type"] = "alien" }));
        Assert.Throws<FormatException>(() => serializer.Read(new JsonObject()));
        Assert.Throws<ArgumentException>(() => message.Write(new DelegateStep(() => { })));
        Assert.Throws<ArgumentNullException>(() => serializer.Write(null!));
        Assert.Throws<ArgumentNullException>(() => serializer.Read(null!));
        Assert.Throws<ArgumentNullException>(() => new CommandJsonSerializer(null!));
        Assert.Throws<ArgumentNullException>(() => new MessageCommandSerializer(null!));
    }

    [Fact]
    public void Item_Serializer_And_Commands_Check_Their_Arguments()
    {
        var codec = new JsonValueCodec();
        var schema = new IoCItemSchema();
        var items = new ItemSerializer(codec, schema);

        Assert.Throws<ArgumentNullException>(() => new ItemSerializer(null!, schema));
        Assert.Throws<ArgumentNullException>(() => new ItemSerializer(codec, null!));
        Assert.Throws<ArgumentNullException>(() => items.Write(null!));
        Assert.Throws<ArgumentNullException>(() => items.Read(null!));
        Assert.Throws<ArgumentException>(() => schema.PropertiesOf(" "));
        Assert.Throws<ArgumentNullException>(() => new RestoreItemsCommand(null!, () => null!, items, new GameItemFactory()));
        Assert.Throws<ArgumentNullException>(() => new RestoreItemsCommand([], null!, items, new GameItemFactory()));
        Assert.Throws<ArgumentNullException>(() => new RestoreItemsCommand([], () => null!, null!, new GameItemFactory()));
        Assert.Throws<ArgumentNullException>(() => new RestoreItemsCommand([], () => null!, items, null!));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<ICollection<ICommandSerializer>>(SerializationDependencies.CommandSerializers, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IValueCodecRegistry>(SerializationDependencies.CodecRegistry, 1));
    }

    [Fact]
    public void Game_And_Queue_Expose_Pending_Commands_Without_Consuming_Them()
    {
        var queue = new GameQueue();
        var command = new DelegateStep(() => { });
        queue.Enqueue(command);

        Assert.Same(command, Assert.Single(queue.Pending));
        Assert.True(queue.HasCommands);
    }
}
