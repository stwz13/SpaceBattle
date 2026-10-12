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
        new RegisterIoCDependencyExceptionFactory().Execute();
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
