namespace SpaceBattle.Lib;

internal readonly record struct MovementSegment
{
    public MovementSegment(Vector start, Vector end)
    {
        if (start.Dim != 2 || end.Dim != 2)
        {
            throw new ArgumentException("A movement segment requires two-dimensional positions.");
        }

        StartX = start.Coordinates[0];
        StartY = start.Coordinates[1];
        EndX = end.Coordinates[0];
        EndY = end.Coordinates[1];
    }

    public int StartX { get; }
    public int StartY { get; }
    public int EndX { get; }
    public int EndY { get; }

    public long DeltaX => (long)EndX - StartX;
    public long DeltaY => (long)EndY - StartY;
}
