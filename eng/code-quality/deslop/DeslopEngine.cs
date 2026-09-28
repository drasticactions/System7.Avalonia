using System.Text;

namespace Proscenium.CodeQuality.Deslop;

internal static partial class DeslopEngine
{
    public static DeslopAnalysis Analyze(IReadOnlyList<DeslopFile> files, int minNodes)
    {
        var fps = new List<DeslopFingerprint>();
        var signatures = new List<ulong[]>();
        foreach (var file in files.Where(file => !file.Skipped).OrderBy(file => file.Path, DeslopPathComparer.Instance).ThenBy(file => file.Root.FileId))
        {
            var found = Fingerprints(file.Root, minNodes);
            signatures.AddRange(Signatures(file.Root, found));
            fps.AddRange(found);
        }
        var pairs = Candidates(fps, signatures);
        Rescue(pairs, fps, files);
        var families = Closure(pairs);
        ContentGate(pairs, fps, files);
        var groups = Split(Closure(pairs), fps, files);
        var familyOf = families.SelectMany((f, i) => f.Select(member => (member, i))).ToDictionary(p => p.member, p => p.i);
        var familyById = new Dictionary<DeslopCluster, List<DeslopFingerprint>>();
        var ranked = new List<DeslopCluster>();
        int hidden = 0;
        foreach (var group in groups)
        {
            var members = Collapse(group, fps, files).Select(i => fps[i]).ToList();
            if (members.Count < 2)
                continue;
            members = DeslopNoise.PruneCSharpPatterns(members, files);
            if (members.Count < 2)
            {
                hidden++;
                continue;
            }
            var cluster = Materialize(members, files);
            ranked.Add(cluster);
            if (familyOf.TryGetValue(group[0], out int family))
                familyById[cluster] = families[family].Select(i => fps[i]).ToList();
        }
        ranked = ranked.OrderByDescending(c => c.Mass).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        ranked = Subsume(ranked);
        var visible = new List<DeslopCluster>();
        foreach (var cluster in ranked)
        {
            bool noise = DeslopNoise.IsNoise(cluster.Members, files);
            if (!noise && familyById.TryGetValue(cluster, out var family))
            {
                var hashes = cluster.Members.Select(m => m.HashKey).ToHashSet();
                var same = family.Where(m => hashes.Contains(m.HashKey)).ToList();
                noise = same.Count > cluster.Members.Count && DeslopNoise.IsNoise(same, files) && !Escapes(cluster.Members, files);
            }
            if (noise)
                hidden++;
            else
                visible.Add(cluster);
        }
        return new(visible, hidden);
    }

    private static string TextKey(DeslopFingerprint member, IReadOnlyList<DeslopFile> files) => Convert.ToBase64String(files[member.FileId].Source, member.Start, member.End - member.Start);
    private static bool Escapes(IReadOnlyList<DeslopFingerprint> members, IReadOnlyList<DeslopFile> files) => members.Select(m => (m.FileId, m.Start, m.End)).Distinct().Count() >= 2 && members.Select(m => m.FileId).Distinct().Count() >= 2 && members.Select(m => TextKey(m, files)).Distinct().Count() == 1;
    private static List<List<int>> Split(List<List<int>> groups, List<DeslopFingerprint> fps, IReadOnlyList<DeslopFile> files)
    {
        var result = new List<List<int>>();
        foreach (var group in groups)
        {
            var families = group.GroupBy(i => TextKey(fps[i], files)).Select(g => g.ToList()).Where(g => g.Select(i => (fps[i].FileId, fps[i].Start, fps[i].End)).Distinct().Count() >= 2).OrderBy(g => g[0]).ToList();
            bool splittable = families.Count > 0 && !(families.Count == 1 && families[0].Count == group.Count);
            if (splittable && DeslopNoise.IsNoise(group.Select(i => fps[i]).ToList(), files))
            {
                var keep = families.Where(g => g.Select(i => fps[i].FileId).Distinct().Count() >= 2).ToList();
                if (keep.Count > 0)
                { result.AddRange(keep); continue; }
            }
            result.Add(group);
        }
        return result;
    }
    private static DeslopCluster Materialize(List<DeslopFingerprint> members, IReadOnlyList<DeslopFile> files)
    {
        int nodes = members.Min(m => m.NodeCount);
        var smallest = members.MinBy(m => m.HashKey, StringComparer.Ordinal)!;
        var chunks = new List<byte[]> { smallest.Hash };
        foreach (var path in members.Select(m => files[m.FileId].Path).Order(DeslopPathComparer.Instance))
        { chunks.Add(Encoding.UTF8.GetBytes(path)); chunks.Add(new byte[1]); }
        return new DeslopCluster { Id = Convert.ToHexStringLower(Digest(chunks).AsSpan(0, 8)), Members = members, CanonicalNodeCount = nodes, Mass = (ulong)nodes * (ulong)(members.Count - 1) };
    }
    private static List<int> Collapse(List<int> group, List<DeslopFingerprint> fps, IReadOnlyList<DeslopFile> files)
    {
        var result = new List<int>();
        foreach (var bucket in group.GroupBy(i => fps[i].FileId).OrderBy(g => g.Key))
        {
            int representative = -1, end = 0;
            foreach (int index in bucket.OrderBy(i => fps[i].Start).ThenByDescending(i => fps[i].End))
            {
                var candidate = fps[index];
                if (representative < 0 || candidate.Start >= end)
                {
                    if (representative >= 0)
                        result.Add(representative);
                    representative = index;
                    end = candidate.End;
                    continue;
                }
                end = Math.Max(end, candidate.End);
                if (Displaces(candidate, fps[representative]))
                    representative = index;
            }
            if (representative >= 0)
                result.Add(representative);
        }
        result.Sort();
        return result;
    }
    private static bool Displaces(DeslopFingerprint candidate, DeslopFingerprint incumbent)
        => candidate.End - candidate.Start > incumbent.End - incumbent.Start;
}
