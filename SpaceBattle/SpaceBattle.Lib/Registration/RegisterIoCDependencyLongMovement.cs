using System.Runtime.CompilerServices;
using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyLongMovement : ICommand
{
    private readonly ICommandReceiver _receiver;
    private readonly Func<IGameItem, ICommand> _createStep;
    private readonly Action<IGameItem, Vector, Vector>? _observe;

    public RegisterIoCDependencyLongMovement(
        ICommandReceiver receiver, Action<IGameItem, Vector, Vector>? observe = null)
        : this(receiver, ConfiguredMovement, observe) { }

    internal RegisterIoCDependencyLongMovement(ICommandReceiver receiver,
        Func<IGameItem, ICommand> createStep, Action<IGameItem, Vector, Vector>? observe)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(createStep);
        _receiver = receiver;
        _createStep = createStep;
        _observe = observe;
    }

    public void Execute()
    {
        var operations = new ConditionalWeakTable<IGameItem, LongOperationCommands>();
        IoCRegistrar.Register(MovementDependencies.CreateOperation, args => args is [IGameItem item]
            ? operations.GetValue(item, Create)
            : throw new ArgumentException("A movement operation requires a game item."));
    }

    internal static ICommand ConfiguredMovement(IGameItem item) =>
        new CallbackCommand(() => Ioc.Resolve<ICommand>("Commands.Action", "Movement", item).Execute());

    private LongOperationCommands Create(IGameItem item)
    {
        var step = _createStep(item);
        if (_observe is { } observe)
        {
            step = new ObserveMovementStepCommand(step,
                () => Ioc.Resolve<IMoving>(GeneratedNames.AdapterDependency(typeof(IMoving)), item).Position,
                (start, end) => observe(item, start, end));
        }

        return Ioc.Resolve<LongOperationCommands>(LongOperationDependencies.Create, step, _receiver);
    }
}
