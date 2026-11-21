using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class GameObjectRuleSchemaTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("""{"turn":{"view":"Unknown","property":"AngularVelocity","type":"angleSteps"}}""")]
    [InlineData("""{"turn":{"view":"SpaceBattle.Lib.IRotating","property":"AngularVelocity","type":"integer"}}""")]
    [InlineData("""
        {
          "turn":{"view":"SpaceBattle.Lib.IRotating","property":"AngularVelocity","type":"angleSteps"},
          "rotation":{"view":"SpaceBattle.Lib.IRotating","property":"AngularVelocity","type":"angleSteps"}
        }
        """)]
    public void Invalid_Schema_Is_Rejected_Before_The_Game_Starts(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            Assert.Throws<FormatException>(() => GameObjectRuleSchema.Load(path));
        }
        finally { File.Delete(path); }
    }
}
