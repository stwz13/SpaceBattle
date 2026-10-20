using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameItemFactoryTests
{
    private readonly IGameItemFactory _factory = new GameItemFactory();

    [Fact]
    public void Empty_Item_Allows_Adding_And_Replacing_Properties()
    {
        var item = _factory.Create();

        item.SetProperty("Count", 1);
        Assert.Equal(1, item.GetProperty("Count"));
        item.SetProperty("Count", 2);
        Assert.Equal(2, item.GetProperty("Count"));
    }

    [Fact]
    public void Arbitrary_Property_Names_And_Value_Types_Need_No_Factory_Changes()
    {
        var properties = new Dictionary<string, object>
        {
            ["Position"] = new Vector(2, 3),
            ["Heading"] = new Angle(1),
            ["Label"] = "example",
            ["Enabled"] = true,
            ["CustomValue"] = 17
        };

        var item = _factory.Create(properties);

        Assert.All(properties, property => Assert.Equal(property.Value, item.GetProperty(property.Key)));
        Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Speed"));
    }

    [Fact]
    public void Changing_Source_Properties_Does_Not_Change_The_Created_Item()
    {
        var properties = new Dictionary<string, object> { ["Value"] = 1 };
        var item = _factory.Create(properties);

        properties["Value"] = 2;
        properties["NewValue"] = 3;
        properties.Clear();

        Assert.Equal(1, item.GetProperty("Value"));
        Assert.Throws<KeyNotFoundException>(() => item.GetProperty("NewValue"));
    }

    [Fact]
    public void Changing_The_Item_Does_Not_Change_Source_Properties()
    {
        var properties = new Dictionary<string, object> { ["Value"] = 1 };
        var item = _factory.Create(properties);

        item.SetProperty("Value", 2);
        item.SetProperty("Extra", 3);

        Assert.Single(properties);
        Assert.Equal(1, properties["Value"]);
    }

    [Fact]
    public void Items_Created_From_The_Same_Properties_Have_Independent_Storage()
    {
        var properties = new Dictionary<string, object> { ["Value"] = 1 };
        var first = _factory.Create(properties);
        var second = _factory.Create(properties);

        first.SetProperty("Value", 2);
        first.SetProperty("Extra", 3);

        Assert.NotSame(first, second);
        Assert.Equal(1, second.GetProperty("Value"));
        Assert.Throws<KeyNotFoundException>(() => second.GetProperty("Extra"));
    }

    [Fact]
    public void Empty_Items_Have_Independent_Storage()
    {
        var first = _factory.Create();
        var second = _factory.Create();

        first.SetProperty("Value", 1);

        Assert.NotSame(first, second);
        Assert.Throws<KeyNotFoundException>(() => second.GetProperty("Value"));
    }

    [Fact]
    public void Property_Values_Preserve_Their_References()
    {
        var sharedService = new object();
        var properties = new Dictionary<string, object> { ["Service"] = sharedService };

        var first = _factory.Create(properties);
        var second = _factory.Create(properties);

        Assert.Same(sharedService, first.GetProperty("Service"));
        Assert.Same(sharedService, second.GetProperty("Service"));
    }

    [Fact]
    public void Property_Names_Are_Case_Sensitive_Regardless_Of_Source_Comparer()
    {
        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["Name"] = "value" };
        var item = _factory.Create(properties);

        item.SetProperty("name", "other");

        Assert.Equal("value", item.GetProperty("Name"));
        Assert.Equal("other", item.GetProperty("name"));
    }

    [Fact]
    public void Lazy_Properties_Are_Enumerated_Once_At_Creation()
    {
        var enumerations = 0;
        IEnumerable<KeyValuePair<string, object>> Properties()
        {
            enumerations++;
            yield return new KeyValuePair<string, object>("Value", enumerations);
        }

        var item = _factory.Create(Properties());

        Assert.Equal(1, item.GetProperty("Value"));
        Assert.Equal(1, item.GetProperty("Value"));
        Assert.Equal(1, enumerations);
    }

    [Fact]
    public void Missing_Property_Fails_Without_Preventing_Subsequent_Writes()
    {
        var item = _factory.Create();

        Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Missing"));
        item.SetProperty("Missing", 1);

        Assert.Equal(1, item.GetProperty("Missing"));
    }

    [Fact]
    public void Null_Properties_Are_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => _factory.Create(null!));
    }

    [Fact]
    public void Duplicate_Property_Names_Are_Rejected()
    {
        var properties = new[]
        {
            new KeyValuePair<string, object>("Value", 1),
            new KeyValuePair<string, object>("Value", 2)
        };

        Assert.Throws<ArgumentException>(() => _factory.Create(properties));
    }
}
