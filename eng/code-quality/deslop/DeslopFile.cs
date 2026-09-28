using Microsoft.CodeAnalysis;

namespace Proscenium.CodeQuality.Deslop;

internal sealed class DeslopFile
{
    public required string Path { get; init; }
    public required byte[] Source { get; init; }
    public required DeslopNode Root { get; init; }
    public required SyntaxTree SyntaxTree { get; init; }
    public bool Skipped { get; init; }
    public int FileId => Root.FileId;
}
