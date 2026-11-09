using SpaceBattle.Lib;

namespace SpaceBattle.Tests.Extensions;

public interface IViewA { int Value { get; } int AResult { set; } }
public interface IViewB { int Value { get; } int BResult { set; } }
public interface ITextView { string Value { get; } string TextResult { set; } }
public class ReadACommand(IViewA view) : ICommand { public void Execute() => view.AResult = view.Value; }
public class ReadBCommand(IViewB view) : ICommand { public void Execute() => view.BResult = view.Value; }
public class ReadTextCommand(ITextView view) : ICommand { public void Execute() => view.TextResult = view.Value; }

public interface IBaseView { int InheritedValue { get; } int BaseResult { set; } }
public interface IDerivedView : IBaseView { int DerivedResult { set; } }
public class ReadBaseCommand(IBaseView view) : ICommand { public void Execute() => view.BaseResult = view.InheritedValue; }
public class ReadDerivedCommand(IDerivedView view) : ICommand { public void Execute() => view.DerivedResult = view.InheritedValue; }

public interface IHiddenBase { int Value { get; } int IntResult { set; } }
public interface IHiddenView : IHiddenBase { new string Value { get; } string HiddenResult { set; } }
public class ReadHiddenCommand(IHiddenView view) : ICommand
{
    public void Execute()
    {
        view.IntResult = ((IHiddenBase)view).Value;
        view.HiddenResult = view.Value;
    }
}

public interface ICounterView { int Counter { get; set; } int Step { get; } }
public class IncrementCommand(ICounterView view) : ICommand
{
    public void Execute() => view.Counter += view.Step;
}

public interface IRecordedCounterView { int Counter { get; } int RecordedCounter { set; } }
public class RecordCounterCommand(IRecordedCounterView view) : ICommand
{
    public void Execute() => view.RecordedCounter = view.Counter;
}

public class Outer<T> { public class Inner<U> { } }
public interface INestedView { Outer<int>.Inner<string> Nested { get; } }
public class ReadNestedCommand(INestedView view) : ICommand { public void Execute() { _ = view.Nested; } }
