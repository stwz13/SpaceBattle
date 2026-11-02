using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGameQuantum : ICommand
{
    private readonly TimeSpan _duration;
    private readonly TimeProvider _time;

    public RegisterIoCDependencyGameQuantum(TimeSpan duration) : this(duration, TimeProvider.System)
    {
    }

    public RegisterIoCDependencyGameQuantum(TimeSpan duration, TimeProvider time)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(time);
        _duration = duration;
        _time = time;
    }

    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", GameDependencies.Quantum,
        (object[] args) => new TimeQuantum(_duration, _time)).Execute();
}
