namespace SpaceBattle.Lib;

public interface ITransactionUndo
{
    void Record(Action undo);
}
