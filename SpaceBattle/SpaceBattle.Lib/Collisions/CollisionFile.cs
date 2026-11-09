namespace SpaceBattle.Lib;

public static class CollisionFile
{
    public const int VectorLength = 4;

    public static IEnumerable<int[]> Parse(IEnumerable<string> lines) =>
        lines
            .Select((text, index) => (Text: text.Trim(), Number: index + 1))
            .Where(line => line.Text.Length > 0 && !line.Text.StartsWith('#'))
            .Select(line => ParseLine(line.Text, line.Number));

    private static int[] ParseLine(string text, int number)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != VectorLength || !parts.All(part => int.TryParse(part, out _)))
        {
            throw new FormatException($"Line {number}: expected {VectorLength} integers (dx dy dvx dvy), got '{text}'.");
        }
        return parts.Select(int.Parse).ToArray();
    }
}
