using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class FactoryRegistrationTests
{
    public FactoryRegistrationTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyValueFactories().Execute();
        new RegisterIoCDependencyExceptionFactory().Execute();
    }

    [Fact]
    public void Resolved_Values_Support_Arithmetic_Without_Constructing_Dependencies_In_Subject_Code()
    {
        var firstVector = Ioc.Resolve<Vector>("Utility.Vector", new int[] { 1, 2 });
        var secondVector = Ioc.Resolve<IVectorFactory>("Factories.Vector").Create([3, 4]);
        var firstAngle = Ioc.Resolve<Angle>("Utility.Angle", 7);
        var secondAngle = Ioc.Resolve<IAngleFactory>("Factories.Angle").Create(2);

        Assert.Equal(TestValues.Vector([4, 6]), firstVector + secondVector);
        Assert.Equal(TestValues.Angle(1), firstAngle + secondAngle);
    }

    [Fact]
    public void Registered_Exception_Factory_Creates_An_Error_With_The_Provided_Message()
    {
        var factory = Ioc.Resolve<IExceptionFactory>("Factories.Exception");

        var error = factory.InvalidOperation("Action rejected");

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal("Action rejected", error.Message);
    }
}
