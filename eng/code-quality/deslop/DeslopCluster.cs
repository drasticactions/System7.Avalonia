namespace Proscenium.CodeQuality.Deslop;

internal sealed class DeslopCluster
{
    public required string Id { get; init; }
    public required List<DeslopFingerprint> Members { get; init; }
    public required int CanonicalNodeCount { get; init; }
    public required ulong Mass { get; init; }
}
