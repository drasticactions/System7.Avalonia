namespace Proscenium.CodeQuality.Deslop;

internal static partial class DeslopEngine
{
    internal sealed record Pair(int Left, int Right, double Structural, double Jaccard, int Smaller, int Larger)
    {
        public double Shared { get; set; }
        public bool RescueEligible => Structural <= 0 && Jaccard < .85 && Jaccard >= .5 && Smaller >= 30 && Smaller >= Larger - Larger / 4;
        public bool Survives => (Structural >= .85 || Jaccard >= .85 || Rescued) && (Structural > 0 || Larger <= (long)Smaller * 4) && (Structural > 0 || Rescued || Jaccard >= .90);
        private bool Rescued => Shared >= .75 && Jaccard >= .5 && Smaller >= 30;
    }

    // Rust pair::candidates::builder::PairBuilder and lsh::banding::emit_sorted_star.
    internal static List<Pair> Candidates(IReadOnlyList<DeslopFingerprint> fps, IReadOnlyList<ulong[]> signatures)
    {
        var evidence = new Dictionary<(int, int), double>();
        void Add(int a, int b, double structural) => evidence[(Math.Min(a, b), Math.Max(a, b))] = structural;
        foreach (var bucket in Enumerable.Range(0, fps.Count).GroupBy(i => fps[i].HashKey))
        {
            var indices = bucket.ToArray();
            int canonical = indices[0];
            int foreign = indices.FirstOrDefault(i => fps[i].FileId != fps[canonical].FileId, -1);
            foreach (int other in indices.Skip(1))
            {
                Add(canonical, other, 1);
                if (foreign >= 0 && fps[other].FileId == fps[canonical].FileId)
                    Add(other, foreign, 1);
            }
        }
        for (int band = 0; band < 32; band++)
        {
            int start = band * 4;
            foreach (var group in Enumerable.Range(0, signatures.Count).GroupBy(i => (signatures[i][start], signatures[i][start + 1], signatures[i][start + 2], signatures[i][start + 3])))
            {
                int canonical = group.First();
                foreach (int other in group.Skip(1))
                    evidence.TryAdd((canonical, other), 0);
            }
        }
        var pairs = new List<Pair>();
        foreach (var (key, structural) in evidence.OrderBy(p => p.Key))
        {
            var (l, r) = key;
            var left = fps[l];
            var right = fps[r];
            if (structural <= 0 && left.FileId == right.FileId && Intersects(left, right))
                continue;
            double j = Enumerable.Range(0, 128).Count(i => signatures[l][i] == signatures[r][i]) / 128.0;
            var p = new Pair(l, r, structural, j, Math.Min(left.NodeCount, right.NodeCount), Math.Max(left.NodeCount, right.NodeCount));
            if (p.Survives || p.RescueEligible && left.FileId != right.FileId)
                pairs.Add(p);
        }
        return pairs;
    }

    internal static List<List<int>> Closure(IReadOnlyList<Pair> pairs)
    {
        var parents = new Dictionary<int, int>();
        int Find(int i)
        {
            parents.TryAdd(i, i);
            int root = i;
            while (parents[root] != root)
                root = parents[root];
            while (parents[i] != i)
            { int next = parents[i]; parents[i] = root; i = next; }
            return root;
        }
        foreach (var p in pairs.Where(p => p.Survives))
        { int a = Find(p.Left), b = Find(p.Right); if (a != b) parents[a] = b; }
        return parents.Keys.ToArray().GroupBy(Find).OrderBy(g => g.Key).Select(g => g.Order().ToList()).ToList();
    }

    private static bool Covers(DeslopFingerprint a, DeslopFingerprint b) => a.FileId == b.FileId && a.Start <= b.Start && a.End >= b.End;
    private static bool Intersects(DeslopFingerprint a, DeslopFingerprint b) => a.Start < b.End && b.Start < a.End;
    private static void Rescue(List<Pair> pairs, List<DeslopFingerprint> fps, IReadOnlyList<DeslopFile> files)
    {
        foreach (var p in pairs.Where(p => p.RescueEligible))
        {
            var l = fps[p.Left];
            var r = fps[p.Right];
            if (l.FileId == r.FileId)
                continue;
            p.Shared = Overlap(l, r, files, true);
            if (p.Shared < .75)
                continue;
            if (DeslopContent.Measure(l, r, files).Agreement < .10)
                p.Shared = 0;
        }
    }
    private static void ContentGate(List<Pair> pairs, List<DeslopFingerprint> fps, IReadOnlyList<DeslopFile> files)
    {
        pairs.RemoveAll(p =>
        {
            var l = fps[p.Left];
            var r = fps[p.Right];
            bool lsh = l.HashKey != r.HashKey && p.Shared < .75 && p.Jaccard >= .90;
            // Structural rescue proposes candidates; it does not establish that
            // their implementations agree. Every candidate needs content evidence.
            var evidence = DeslopContent.Measure(l, r, files);
            return !evidence.Measured || evidence.Agreement < (lsh ? .85 : .7);
        });
    }
}
