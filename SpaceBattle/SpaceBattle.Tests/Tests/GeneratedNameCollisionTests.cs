using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests.Collision_Name
{
    public interface IView { int Value { get; } }

    public sealed class ReadCommand(IView view) : Lib.ICommand
    {
        public void Execute() => Assert.Equal(1, view.Value);
    }
}

namespace SpaceBattle.Tests.Collision.Name
{
    public interface IView { int Value { get; } }

    public sealed class ReadCommand(IView view) : Lib.ICommand
    {
        public void Execute() => Assert.Equal(2, view.Value);
    }
}

namespace SpaceBattle.Tests
{
    public class GeneratedNameCollisionTests
    {
        [Fact]
        public void Generates_Separate_Strategies_For_Types_That_Used_To_Share_A_Name()
        {
            new InitCommand().Execute();
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
            new RegisterIoCDependencyCodeGeneration().Execute();
            new RegisterIoCDependencyGeneratedStrategies(typeof(GeneratedNameCollisionTests).Assembly, []).Execute();

            var first = new TestGameItem();
            first.SetProperty("Value", 1);
            var second = new TestGameItem();
            second.SetProperty("Value", 2);
            Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(Collision_Name.ReadCommand)), first).Execute();
            Ioc.Resolve<Lib.ICommand>(GeneratedNames.CommandDependency(typeof(Collision.Name.ReadCommand)), second).Execute();
        }
    }
}
