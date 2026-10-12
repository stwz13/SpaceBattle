using Microsoft.CodeAnalysis.CSharp;
using SpaceBattle.Lib;
using SpaceBattle.Tests.Extensions;

namespace SpaceBattle.Tests;

public class GeneratedNamesTests
{
    [Theory]
    [InlineData("Handler", "", "Handler_SpaceBattle_Lib_IGameItem")]
    [InlineData("Serializer", "Strategy", "Serializer_SpaceBattle_Lib_IGameItemStrategy")]
    [InlineData("", "Receiver", "SpaceBattle_Lib_IGameItemReceiver")]
    public void Identifier_Supports_New_Entity_Kinds(string prefix, string suffix, string expected)
    {
        var name = GeneratedNames.Identifier(prefix, typeof(IGameItem), suffix);

        Assert.Equal(expected, name);
        Assert.True(SyntaxFacts.IsValidIdentifier(name));
    }

    [Theory]
    [InlineData(DependencyNamespaces.Adapters, "Adapter", "Adapters.SpaceBattle_Lib_IGameItemAdapter")]
    [InlineData(DependencyNamespaces.Commands, "", "Commands.SpaceBattle_Lib_IGameItem")]
    [InlineData(DependencyNamespaces.Properties, "", "Properties.SpaceBattle_Lib_IGameItem")]
    [InlineData("Handlers", "", "Handlers.SpaceBattle_Lib_IGameItem")]
    [InlineData("Serializers", "Strategy", "Serializers.SpaceBattle_Lib_IGameItemStrategy")]
    public void DependencyKey_Supports_New_Categories(string category, string suffix, string expected)
    {
        Assert.Equal(expected, GeneratedNames.DependencyKey(category, typeof(IGameItem), suffix));
    }

    [Fact]
    public void DependencyKey_Composes_An_Arbitrary_Number_Of_Parts()
    {
        Assert.Equal("Handlers.Collision.Ship.Torpedo",
            GeneratedNames.DependencyKey("Handlers", "Collision", "Ship", "Torpedo"));
    }

    [Fact]
    public void Existing_Adapter_And_Command_Names_Remain_Unchanged()
    {
        Assert.Equal("SpaceBattle_Tests_Extensions_ICounterViewAdapter",
            GeneratedNames.AdapterClass(typeof(ICounterView)));
        Assert.Equal("Adapters.SpaceBattle_Tests_Extensions_ICounterViewAdapter",
            GeneratedNames.AdapterDependency(typeof(ICounterView)));
        Assert.Equal("Adapter_SpaceBattle_Tests_Extensions_ICounterViewAdapter",
            GeneratedNames.AdapterMethod(typeof(ICounterView)));
        Assert.Equal("Commands.SpaceBattle_Tests_Extensions_IncrementCommand",
            GeneratedNames.CommandDependency(typeof(IncrementCommand)));
        Assert.Equal("Command_SpaceBattle_Tests_Extensions_IncrementCommand",
            GeneratedNames.CommandMethod(typeof(IncrementCommand)));
        Assert.Equal("SpaceBattle.Generated.Strategies", GeneratedNames.StrategiesFullName);
    }

    [Fact]
    public void Existing_Property_Keys_Include_The_View_And_Declaring_Interface()
    {
        var property = typeof(IBaseView).GetProperty(nameof(IBaseView.InheritedValue))!;

        Assert.Equal("Properties.SpaceBattle_Tests_Extensions_IDerivedView.SpaceBattle_Tests_Extensions_IBaseView.InheritedValue",
            GeneratedNames.PropertyDependency(typeof(IDerivedView), property));
    }

    [Fact]
    public void Nested_Type_Names_Are_Valid_Identifiers()
    {
        var name = GeneratedNames.Identifier("Handler", typeof(NestedEntity));

        Assert.Equal("Handler_SpaceBattle_Tests_GeneratedNamesTests_NestedEntity", name);
        Assert.True(SyntaxFacts.IsValidIdentifier(name));
    }

    public class NestedEntity
    {
    }
}
