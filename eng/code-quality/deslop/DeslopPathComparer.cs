using System.Text;

namespace Proscenium.CodeQuality.Deslop;

internal sealed class DeslopPathComparer : IComparer<string>
{
    public static DeslopPathComparer Instance { get; } = new();

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        // Rust Path::cmp compares components, not the displayed path string.
        // Thus A/file.cs sorts before A.Core/file.cs, although '/' follows '.'.
        var leftParts = left.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rightParts = right.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < Math.Min(leftParts.Length, rightParts.Length); index++)
        {
            int comparison = Encoding.UTF8.GetBytes(leftParts[index]).AsSpan()
                .SequenceCompareTo(Encoding.UTF8.GetBytes(rightParts[index]));
            if (comparison != 0)
                return comparison;
        }
        return leftParts.Length.CompareTo(rightParts.Length);
    }
}
