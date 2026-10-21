using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameRegistrationTests
{
    public GameRegistrationTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyBattleContextFactory().Execute();
        new RegisterIoCDependencyGameQuantum(TimeSpan.FromHours(1)).Execute();
        new RegisterIoCDependencyGame().Execute();
    }

    private sealed class Step(Action action) : Lib.ICommand
    {
        public void Execute() => action();
    }

    private sealed class CountingQuantum : IQuantum
    {
        public int Starts { get; private set; }
        public bool IsOver => true;
        public void Start() => Starts++;
    }

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static IGame Create(string id) => Ioc.Resolve<IGame>(GameDependencies.Create, id);

    private static IBattleContext Context() => Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context);

    private static Lib.ICommand Run(Action action) => new Step(action);

    private static T InChildScope<T>(Func<T> body)
    {
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        try
        {
            return body();
        }
        finally
        {
            SetScope(parent);
        }
    }

    [Fact]
    public void Created_Game_Has_The_Given_Identifier_And_No_Commands()
    {
        var game = Create("Battle");

        Assert.Equal("Battle", game.Id);
        Assert.False(game.HasCommands);
    }

    [Fact]
    public void Each_Game_Is_A_New_Object()
    {
        Assert.NotSame(Create("Battle"), Create("Battle"));
    }

    [Fact]
    public void Game_Commands_See_The_Battle_Context_Of_Their_Own_Game()
    {
        var game = Create("Battle");
        string? seen = null;
        game.Post(Run(() => seen = Context().Id));

        game.Execute();

        Assert.Equal("Battle", seen);
    }

    [Fact]
    public void Games_Have_Separate_Contexts_And_Storages()
    {
        var first = Create("First");
        var second = Create("Second");
        IGameItemStorage? firstStorage = null;
        IGameItemStorage? secondStorage = null;
        first.Post(Run(() =>
        {
            firstStorage = Context().Storage;
            Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage).Add("Ship", new TestGameItem());
        }));
        second.Post(Run(() =>
        {
            secondStorage = Context().Storage;
            Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage).Add("Ship", new TestGameItem());
        }));

        first.Execute();
        second.Execute();

        Assert.NotSame(firstStorage, secondStorage);
        Assert.Single(firstStorage!.GetSnapshot());
        Assert.Single(secondStorage!.GetSnapshot());
    }

    [Fact]
    public void Game_State_Lives_Between_Executions()
    {
        var game = Create("Battle");
        game.Post(Run(() =>
            Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage).Add("Ship", new TestGameItem())));
        game.Execute();
        var found = false;
        game.Post(Run(() =>
            found = Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage).Get("Ship") != null));

        game.Execute();

        Assert.True(found);
    }

    [Fact]
    public void Execution_Restores_The_Previous_Scope()
    {
        var game = Create("Battle");
        var outer = Ioc.Resolve<object>("IoC.Scope.Current");
        object? inner = null;
        game.Post(Run(() => inner = Ioc.Resolve<object>("IoC.Scope.Current")));

        game.Execute();

        Assert.NotSame(outer, inner);
        Assert.Same(outer, Ioc.Resolve<object>("IoC.Scope.Current"));
    }

    [Fact]
    public void Failing_Command_Still_Restores_The_Previous_Scope()
    {
        var game = Create("Battle");
        var outer = Ioc.Resolve<object>("IoC.Scope.Current");
        game.Post(Run(() => throw new InvalidOperationException("boom")));

        Assert.Throws<InvalidOperationException>(game.Execute);

        Assert.Same(outer, Ioc.Resolve<object>("IoC.Scope.Current"));
    }

    [Fact]
    public void Game_Scope_Inherits_The_Dependencies_Of_The_Scope_It_Was_Created_In()
    {
        Register("Test.Value", _ => 42);
        var game = Create("Battle");
        var value = 0;
        game.Post(Run(() => value = Ioc.Resolve<int>("Test.Value")));

        game.Execute();

        Assert.Equal(42, value);
    }

    [Fact]
    public void Dependencies_Registered_In_A_Game_Do_Not_Leak_Out()
    {
        var game = Create("Battle");
        game.Post(Run(() => Register("Test.Local", _ => 1)));

        game.Execute();

        Assert.ThrowsAny<Exception>(() => Ioc.Resolve<int>("Test.Local"));
    }

    [Fact]
    public void Games_On_Different_Threads_Each_Run_In_Their_Own_Scope()
    {
        var first = Create("First");
        var second = Create("Second");
        using var meeting = new Barrier(2);
        var met = new List<bool>();
        var seen = new List<string>();
        var gate = new object();
        Action Visit(IGame game) => () =>
        {
            game.Post(Run(() =>
            {
                var together = meeting.SignalAndWait(TimeSpan.FromSeconds(10));
                var id = Context().Id;
                lock (gate)
                {
                    met.Add(together);
                    seen.Add(id);
                }
            }));
            game.Execute();
        };
        var outer = Ioc.Resolve<object>("IoC.Scope.Current");

        Task.WaitAll(Task.Run(Visit(first)), Task.Run(Visit(second)));

        Assert.All(met, together => Assert.True(together));
        Assert.Equal(["First", "Second"], seen.Order().ToArray());
        Assert.Same(outer, Ioc.Resolve<object>("IoC.Scope.Current"));
    }

    [Fact]
    public void Quantum_Is_Taken_From_The_Registered_Dependency_When_The_Game_Is_Created()
    {
        var quantum = new CountingQuantum();
        var game = InChildScope(() =>
        {
            Register(GameDependencies.Quantum, _ => quantum);
            return Create("Battle");
        });
        var executed = 0;
        game.Post(Run(() => executed++));
        game.Post(Run(() => executed++));

        game.Execute();

        Assert.Equal(1, quantum.Starts);
        Assert.Equal(1, executed);
        Assert.True(game.HasCommands);
    }

    [Fact]
    public void Zero_Duration_Quantum_Lets_One_Command_Run_Per_Execution()
    {
        var game = InChildScope(() =>
        {
            new RegisterIoCDependencyGameQuantum(TimeSpan.Zero).Execute();
            return Create("Battle");
        });
        var executed = 0;
        game.Post(Run(() => executed++));
        game.Post(Run(() => executed++));
        game.Post(Run(() => executed++));

        game.Execute();
        Assert.Equal(1, executed);
        game.Execute();
        game.Execute();

        Assert.Equal(3, executed);
        Assert.False(game.HasCommands);
    }

    [Fact]
    public void Long_Quantum_Lets_All_Commands_Run_At_Once()
    {
        var game = Create("Battle");
        var executed = 0;
        game.Post(Run(() => executed++));
        game.Post(Run(() => executed++));
        game.Post(Run(() => executed++));

        game.Execute();

        Assert.Equal(3, executed);
    }

    [Fact]
    public void Registered_Quantum_Uses_The_Given_Time_Source()
    {
        var time = new ManualTimeProvider();
        var game = InChildScope(() =>
        {
            new RegisterIoCDependencyGameQuantum(TimeSpan.FromMilliseconds(100), time).Execute();
            return Create("Battle");
        });
        var executed = 0;
        game.Post(Run(() =>
        {
            executed++;
            time.Advance(60);
        }));
        game.Post(Run(() =>
        {
            executed++;
            time.Advance(60);
        }));
        game.Post(Run(() => executed++));

        game.Execute();

        Assert.Equal(2, executed);
        Assert.True(game.HasCommands);
    }

    [Fact]
    public void Quantum_Registration_Rejects_Invalid_Arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegisterIoCDependencyGameQuantum(TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyGameQuantum(TimeSpan.Zero, null!));
    }

    [Fact]
    public void Game_Needs_Exactly_One_Non_Blank_Identifier()
    {
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGame>(GameDependencies.Create));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGame>(GameDependencies.Create, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGame>(GameDependencies.Create, "A", "B"));
        Assert.Throws<ArgumentException>(() => Create(" "));
    }

    [Fact]
    public void Game_Scope_Needs_A_Scope()
    {
        Assert.Throws<ArgumentNullException>(() => new GameScope(null!));
    }
}
