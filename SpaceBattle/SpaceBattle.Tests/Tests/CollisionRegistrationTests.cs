using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CollisionRegistrationTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("SpaceBattle-Collisions-");

    public CollisionRegistrationTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
    }

    public void Dispose() => _directory.Delete(true);

    private sealed class CountingCommand : Lib.ICommand
    {
        public int Count { get; private set; }
        public void Execute() => Count++;
    }

    private string Write(string relativePath, params string[] lines)
    {
        var path = Path.Combine(_directory.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
        return path;
    }

    private void Register() => new RegisterIoCDependencyCollisions(_directory.FullName).Execute();

    private static TestGameItem Ship(int x, int y, int vx, int vy)
    {
        var ship = new TestGameItem();
        ship.Properties["Position"] = new Vector(x, y);
        ship.Properties["Velocity"] = new Vector(vx, vy);
        return ship;
    }

    private static Lib.ICommand Collision(string tree, IGameItem first, IGameItem second, Lib.ICommand onCollision) =>
        Ioc.Resolve<Lib.ICommand>("Commands.Collision", tree, first, second, onCollision);

    [Fact]
    public void Every_File_Is_Registered_Under_Its_Name_Without_Extension()
    {
        Write("Ship-Torpedo.collisions", "1 2 3 4");
        Write("Ship-Ship/0-0.collisions", "5 6 7 8");
        Write("Ship-Ship/3-1.collisions", "9 0 9 0");
        Write("ignored.txt", "0 0 0 0");
        Register();

        Assert.True(Ioc.Resolve<CollisionTree>("Collisions.Ship-Torpedo").Contains([1, 2, 3, 4]));
        Assert.True(Ioc.Resolve<CollisionTree>("Collisions.Ship-Ship.0-0").Contains([5, 6, 7, 8]));
        Assert.True(Ioc.Resolve<CollisionTree>("Collisions.Ship-Ship.3-1").Contains([9, 0, 9, 0]));
        Assert.False(Ioc.Resolve<CollisionTree>("Collisions.Ship-Ship.0-0").Contains([1, 2, 3, 4]));
        Assert.Throws<Exception>(() => Ioc.Resolve<CollisionTree>("Collisions.ignored"));
    }

    [Fact]
    public void Tree_Is_Read_On_First_Use_And_Cached()
    {
        var path = Write("Pair.collisions", "1 1 1 1");
        Register();
        File.WriteAllLines(path, ["2 2 2 2"]);

        var first = Ioc.Resolve<CollisionTree>("Collisions.Pair");
        File.WriteAllLines(path, ["3 3 3 3"]);
        var second = Ioc.Resolve<CollisionTree>("Collisions.Pair");

        Assert.Same(first, second);
        Assert.True(first.Contains([2, 2, 2, 2]));
        Assert.False(first.Contains([3, 3, 3, 3]));
    }

    [Fact]
    public void Malformed_File_Fails_When_Its_Tree_Is_First_Used_And_Not_When_Registering()
    {
        Write("Broken.collisions", "1 2 3 4", "oops");
        Register();

        Assert.Throws<FormatException>(() => Ioc.Resolve<CollisionTree>("Collisions.Broken"));
    }

    [Fact]
    public void Missing_Directory_Fails_On_Registration()
    {
        var command = new RegisterIoCDependencyCollisions(Path.Combine(_directory.FullName, "missing"));

        Assert.Throws<DirectoryNotFoundException>(command.Execute);
    }

    [Fact]
    public void Collision_Command_Uses_Relative_Position_And_Velocity_Of_Two_Items()
    {
        Write("Ship-Torpedo.collisions", "5 0 -1 0");
        Register();
        var ship = Ship(10, 3, 0, 0);
        var torpedo = Ship(15, 3, -1, 0);
        var consequence = new CountingCommand();

        Collision("Collisions.Ship-Torpedo", ship, torpedo, consequence).Execute();

        Assert.Equal(1, consequence.Count);
    }

    [Theory]
    [InlineData(16, 3, -1, 0)]
    [InlineData(15, 4, -1, 0)]
    [InlineData(15, 3, 0, 0)]
    [InlineData(15, 3, -1, 1)]
    public void Collision_Command_Ignores_Other_Offsets(int x, int y, int vx, int vy)
    {
        Write("Ship-Torpedo.collisions", "5 0 -1 0");
        Register();
        var consequence = new CountingCommand();

        Collision("Collisions.Ship-Torpedo", Ship(10, 3, 0, 0), Ship(x, y, vx, vy), consequence).Execute();

        Assert.Equal(0, consequence.Count);
    }

    [Fact]
    public void Collision_Command_Reads_The_Items_When_It_Is_Executed()
    {
        Write("Ship-Torpedo.collisions", "5 0 -1 0");
        Register();
        var ship = Ship(10, 3, 0, 0);
        var torpedo = Ship(30, 3, -1, 0);
        var consequence = new CountingCommand();
        var command = Collision("Collisions.Ship-Torpedo", ship, torpedo, consequence);

        command.Execute();
        torpedo.Properties["Position"] = new Vector(15, 3);
        command.Execute();

        Assert.Equal(1, consequence.Count);
    }

    [Fact]
    public void Collision_Of_Items_Without_Position_Fails_And_Does_Not_Run_The_Consequence()
    {
        Write("Ship-Torpedo.collisions", "5 0 -1 0");
        Register();
        var torpedo = Ship(15, 3, -1, 0);
        torpedo.Properties.Remove("Position");
        var consequence = new CountingCommand();

        Assert.Throws<KeyNotFoundException>(() =>
            Collision("Collisions.Ship-Torpedo", Ship(10, 3, 0, 0), torpedo, consequence).Execute());
        Assert.Equal(0, consequence.Count);
    }

    [Fact]
    public void Unknown_Tree_Fails_When_The_Command_Is_Created()
    {
        Register();

        Assert.Throws<Exception>(() =>
            Collision("Collisions.Nothing", Ship(0, 0, 0, 0), Ship(1, 1, 1, 1), new CountingCommand()));
    }
}
