using Microsoft.CodeAnalysis;

namespace Proscenium.CodeQuality.Deslop;

internal sealed class DeslopNode
{
    public required string Kind { get; init; }
    public required int Start { get; init; }
    public required int End { get; init; }
    public required int FileId { get; init; }
    public List<DeslopNode> Children { get; init; } = [];
    public SyntaxNode? Syntax { get; init; }
    public DeslopNode? Parent { get; set; }

    public int NodeCount => 1 + Children.Sum(child => child.NodeCount);

    public bool Covers(int start, int end) => Start <= start && end <= End;

    internal IEnumerable<DeslopNode> SelfAndDescendants(
        Func<DeslopNode, bool>? includeChild = null)
    {
        yield return this;
        foreach (var child in Children)
        {
            if (includeChild is not null && !includeChild(child))
                continue;
            foreach (var descendant in child.SelfAndDescendants(includeChild))
                yield return descendant;
        }
    }
}
