namespace Proscenium.CodeQuality.Deslop;

internal sealed record DeslopAnalysis(IReadOnlyList<DeslopCluster> Clusters, int ClustersHidden);
