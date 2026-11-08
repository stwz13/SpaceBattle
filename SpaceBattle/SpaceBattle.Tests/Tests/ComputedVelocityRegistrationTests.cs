using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using SpaceBattle.Tests.VelocityExtensions;

namespace SpaceBattle.Tests;

public class ComputedVelocityRegistrationTests
{
    public ComputedVelocityRegistrationTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyComputedVelocity().Execute();
    }

    private static TestGameItem Item(int speed = 10, int direction = 0)
    {
        var item = new TestGameItem();
        item.Properties["Speed"] = speed;
        item.Properties["Direction"] = new Angle(direction);
        return item;
    }

    private static Vector Velocity(IGameItem item) =>
        Ioc.Resolve<Vector>(ComputedVelocityDependencies.Velocity, item);

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static void Generate(params Dependency[] dependencies)
    {
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(CaptureVelocityCommand).Assembly, dependencies).Execute();
    }

    private static T Adapt<T>(IGameItem item) =>
        Ioc.Resolve<T>(GeneratedNames.AdapterDependency(typeof(T)), item);

    [Theory]
    [InlineData(10, 0, 10, 0)]
    [InlineData(10, 1, 7, 7)]
    [InlineData(10, 2, 0, 10)]
    [InlineData(10, 3, -7, 7)]
    [InlineData(10, 4, -10, 0)]
    [InlineData(10, 5, -7, -7)]
    [InlineData(10, 6, 0, -10)]
    [InlineData(10, 7, 7, -7)]
    [InlineData(1, 1, 1, 1)]
    [InlineData(0, 3, 0, 0)]
    [InlineData(10, -1, 7, -7)]
    [InlineData(10, 9, 7, 7)]
    [InlineData(int.MaxValue, 0, int.MaxValue, 0)]
    [InlineData(int.MaxValue, 4, -int.MaxValue, 0)]
    public void Registered_Dependency_Computes_Rounded_Velocity_Without_Storing_It(
        int speed, int direction, int x, int y)
    {
        var item = Item(speed, direction);

        Assert.Equal(new Vector(x, y), Velocity(item));

        Assert.Equal(2, item.Properties.Count);
        Assert.Equal(speed, item.Properties["Speed"]);
        Assert.Equal(new Angle(direction), item.Properties["Direction"]);
    }

    [Fact]
    public void Resolving_Velocity_Reads_Only_The_Current_Source_Properties()
    {
        var item = new Mock<IGameItem>(MockBehavior.Strict);
        item.Setup(i => i.GetProperty("Speed")).Returns(10);
        item.Setup(i => i.GetProperty("Direction")).Returns(new Angle(1));

        Assert.Equal(new Vector(7, 7), Velocity(item.Object));

        item.Verify(i => i.GetProperty("Speed"), Times.Once);
        item.Verify(i => i.GetProperty("Direction"), Times.Once);
        item.VerifyNoOtherCalls();
    }

    [Fact]
    public void The_Same_Strategy_Supports_Unrelated_Interfaces_And_Different_Target_Property_Names()
    {
        Generate(
            new Dependency(typeof(IVelocitySnapshot), nameof(IVelocitySnapshot.Velocity),
                ComputedVelocityDependencies.Velocity),
            new Dependency(typeof(IDisplacementSnapshot), nameof(IDisplacementSnapshot.Displacement),
                ComputedVelocityDependencies.Velocity));
        var first = Item(10, 1);
        var second = Item(4, 2);

        Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(CaptureVelocityCommand)), first).Execute();
        Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(CaptureDisplacementCommand)), second).Execute();

        Assert.Equal(new Vector(7, 7), first.Properties["VelocitySnapshot"]);
        Assert.Equal(new Vector(0, 4), second.Properties["DisplacementSnapshot"]);
        Assert.False(first.Properties.ContainsKey("Velocity"));
        Assert.False(second.Properties.ContainsKey("Displacement"));
    }

    [Fact]
    public void Existing_Adapter_Recalculates_After_Speed_Or_Direction_Changes()
    {
        Generate(new Dependency(typeof(IVelocitySnapshot), nameof(IVelocitySnapshot.Velocity),
            ComputedVelocityDependencies.Velocity));
        var item = Item();
        var adapter = Adapt<IVelocitySnapshot>(item);
        var first = adapter.Velocity;
        Assert.Equal(new Vector(10, 0), first);

        item.Properties["Speed"] = 4;
        Assert.Equal(new Vector(4, 0), adapter.Velocity);
        item.Properties["Direction"] = new Angle(2);
        Assert.Equal(new Vector(0, 4), adapter.Velocity);
        Assert.Equal(new Vector(10, 0), first);
        Assert.Equal(2, item.Properties.Count);
    }

    [Fact]
    public void Multiple_Source_Schemas_Can_Be_Registered_And_Used_Simultaneously()
    {
        const string alternate = "Properties.AlternateVelocity";
        new RegisterIoCDependencyComputedVelocity(alternate, "TravelSpeed", "Heading").Execute();
        Generate(
            new Dependency(typeof(IVelocitySnapshot), nameof(IVelocitySnapshot.Velocity),
                ComputedVelocityDependencies.Velocity),
            new Dependency(typeof(IDisplacementSnapshot), nameof(IDisplacementSnapshot.Displacement), alternate));
        var item = Item(10, 0);
        item.Properties["TravelSpeed"] = 4;
        item.Properties["Heading"] = new Angle(2);

        Assert.Equal(new Vector(10, 0), Adapt<IVelocitySnapshot>(item).Velocity);
        Assert.Equal(new Vector(0, 4), Adapt<IDisplacementSnapshot>(item).Displacement);
        item.Properties["TravelSpeed"] = 7;
        Assert.Equal(new Vector(0, 7), Adapt<IDisplacementSnapshot>(item).Displacement);
        Assert.Equal(new Vector(10, 0), Adapt<IVelocitySnapshot>(item).Velocity);
    }

    [Fact]
    public void Child_Scope_Can_Change_The_Algorithm_For_Existing_Adapters_Without_Changing_The_Parent()
    {
        Generate(new Dependency(typeof(IVelocitySnapshot), nameof(IVelocitySnapshot.Velocity),
            ComputedVelocityDependencies.Velocity));
        var item = Item();
        var adapter = Adapt<IVelocitySnapshot>(item);
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        try
        {
            Ioc.Resolve<App.ICommand>("IoC.Register", ComputedVelocityDependencies.Velocity,
                (object[] args) => new Vector(1, 2)).Execute();

            Assert.Equal(new Vector(1, 2), adapter.Velocity);
        }
        finally
        {
            SetScope(parent);
        }

        Assert.Equal(new Vector(10, 0), adapter.Velocity);
    }

    [Theory]
    [InlineData("Speed")]
    [InlineData("Direction")]
    public void Missing_Source_Property_Does_Not_Modify_The_Object(string property)
    {
        var item = Item();
        item.Properties.Remove(property);
        var original = new Dictionary<string, object>(item.Properties);

        Assert.Throws<KeyNotFoundException>(() => Velocity(item));

        Assert.Equal(original.Count, item.Properties.Count);
        Assert.All(original, pair => Assert.Same(pair.Value, item.Properties[pair.Key]));
    }

    [Theory]
    [InlineData("Speed")]
    [InlineData("Direction")]
    public void Incorrect_Source_Property_Type_Does_Not_Modify_The_Object(string property)
    {
        var item = Item();
        item.Properties[property] = "invalid";
        var original = new Dictionary<string, object>(item.Properties);

        Assert.Throws<InvalidCastException>(() => Velocity(item));

        Assert.Equal(original.Count, item.Properties.Count);
        Assert.All(original, pair => Assert.Same(pair.Value, item.Properties[pair.Key]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Negative_Speed_Is_Rejected_Without_Changing_Source_Properties(int speed)
    {
        var item = Item(speed);

        Assert.Throws<ArgumentOutOfRangeException>(() => Velocity(item));

        Assert.Equal(2, item.Properties.Count);
        Assert.Equal(speed, item.Properties["Speed"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Invalid_Registration_Names_Are_Rejected(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new RegisterIoCDependencyComputedVelocity(dependencyKey: name!));
        Assert.ThrowsAny<ArgumentException>(() =>
            new RegisterIoCDependencyComputedVelocity(speedProperty: name!));
        Assert.ThrowsAny<ArgumentException>(() =>
            new RegisterIoCDependencyComputedVelocity(directionProperty: name!));
    }

    [Fact]
    public void Custom_Calculation_Uses_The_Current_Item_Properties()
    {
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        var angle = new Angle(2);
        var item = new Mock<IGameItem>(MockBehavior.Strict);
        item.Setup(source => source.GetProperty("Speed")).Returns(10);
        item.Setup(source => source.GetProperty("Direction")).Returns(angle);
        var expected = new Vector(3, 4);
        new RegisterIoCDependencyComputedVelocity((speed, direction) =>
        {
            Assert.Equal(10, speed);
            Assert.Same(angle, direction);
            return expected;
        }).Execute();

        Assert.Same(expected, Ioc.Resolve<Vector>(ComputedVelocityDependencies.Velocity, item.Object));
    }
}
