namespace Proscenium.CodeQuality.Deslop;

internal static class DeslopNoise
{
    public static bool IsNoise(IReadOnlyList<DeslopFingerprint> members, IReadOnlyList<DeslopFile> files)
        => members.Count >= 2 && PruneCSharpPatterns(members.ToList(), files).Count == 0;

    internal static List<DeslopFingerprint> PruneCSharpPatterns(List<DeslopFingerprint> members, IReadOnlyList<DeslopFile> files)
    {
        var classified = members.Select(member => (Member: member, Pattern: DeslopCSharpPatterns.Classify(member, files))).ToArray();
        var copies = classified.Where(item => item.Pattern.Identity is not null)
            .GroupBy(item => item.Pattern.Identity).Where(group => group.Select(item =>
                (item.Member.FileId, item.Member.Start, item.Member.End)).Distinct().Skip(1).Any())
            .Select(group => group.Key).ToHashSet();
        return classified.Where(item => item.Pattern.Identity is null || copies.Contains(item.Pattern.Identity))
            .Select(item => item.Member).ToList();
    }
}
