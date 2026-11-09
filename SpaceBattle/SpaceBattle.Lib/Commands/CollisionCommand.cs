namespace SpaceBattle.Lib;

public class CollisionCommand : ICommand
{
    private readonly CollisionTree _tree;
    private readonly Func<Vector> _offset;
    private readonly ICommand _onCollision;

    public CollisionCommand(CollisionTree tree, Func<Vector> offset, ICommand onCollision)
    {
        _tree = tree;
        _offset = offset;
        _onCollision = onCollision;
    }

    public void Execute()
    {
        if (_tree.Contains(_offset().Coordinates))
        {
            _onCollision.Execute();
        }
    }
}
