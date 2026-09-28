namespace Proscenium.CodeQuality.Deslop;

internal static partial class DeslopEngine
{
    // Rust overlap::{view::build_view, Aligner::distance, credit::credit_shared_nodes}.
    public static double Overlap(DeslopFingerprint left, DeslopFingerprint right, IReadOnlyList<DeslopFile> files, bool rescue = false)
    {
        if (left.HashKey == right.HashKey)
            return 1;
        var ln = RangeNodes(files[left.FileId].Root, left.Start, left.End);
        var rn = RangeNodes(files[right.FileId].Root, right.Start, right.End);
        if (ln is null || rn is null)
            return 0;
        List<(string Kind, int Leaf)> Post(IReadOnlyList<DeslopNode> nodes)
        {
            var result = new List<(string, int)> { ("", 0) };
            int Walk(DeslopNode n)
            {
                int leaf = result.Count;
                foreach (var child in n.Children)
                    Walk(child);
                result.Add((n.Kind, leaf));
                return leaf;
            }
            foreach (var n in nodes)
                Walk(n);
            return result;
        }
        var a = Post(ln);
        var b = Post(rn);
        int na = a.Count - 1, nb = b.Count - 1, larger = Math.Max(na, nb);
        if (larger == 0)
            return 0;
        if (rescue)
        {
            var ka = DeslopFrequency.Count(a.Skip(1), node => node.Kind);
            var kb = DeslopFrequency.Count(b.Skip(1), node => node.Kind);
            double bound = ka.Sum(k => Math.Min(k.Value, kb.GetValueOrDefault(k.Key))) / (double)larger;
            if (bound < .75)
                return bound;
            var row = new int[nb + 1];
            for (int i = 1; i <= na; i++)
            {
                int diagonal = 0;
                for (int j = 1; j <= nb; j++)
                {
                    int old = row[j];
                    row[j] = a[i].Kind == b[j].Kind ? diagonal + 1 : Math.Max(row[j], row[j - 1]);
                    diagonal = old;
                }
            }
            bound = row[nb] / (double)larger;
            if (bound < .75)
                return bound;
        }
        if (larger > 768)
        {
            IEnumerable<DeslopFingerprint> OrderedFingerprints(IEnumerable<DeslopNode> nodes)
                => nodes.SelectMany(n => Fingerprints(n, 3, false))
                    .OrderBy(f => f.Start)
                    .ThenByDescending(f => f.NodeCount);
            var le = OrderedFingerprints(ln);
            var re = OrderedFingerprints(rn).ToLookup(f => f.HashKey);
            int lc = 0, rc = 0, matched = 0;
            foreach (var entry in le)
            {
                if (entry.Start < lc)
                    continue;
                var other = re[entry.HashKey].FirstOrDefault(f => f.Start >= rc);
                if (other is null)
                    continue;
                matched += entry.NodeCount;
                lc = entry.End;
                rc = other.End;
            }
            return Math.Clamp(Math.Max(0, 2 * matched - Math.Min(na, nb)) / (double)larger, 0, 1);
        }
        return Math.Max(0, larger - TreeDistance(a, b)) / (double)larger;
    }

    private static int TreeDistance(List<(string Kind, int Leaf)> a, List<(string Kind, int Leaf)> b)
    {
        a.Add(("__window__", 1));
        b.Add(("__window__", 1));
        int[] Keys(List<(string Kind, int Leaf)> nodes) => nodes.Select((n, i) => (n.Leaf, i)).Skip(1).GroupBy(p => p.Leaf).Select(g => g.Last().i).Order().ToArray();
        var distance = new int[a.Count, b.Count];
        foreach (int ar in Keys(a))
            foreach (int br in Keys(b))
            {
                int al = a[ar].Leaf, bl = b[br].Leaf;
                var forest = new int[ar - al + 2, br - bl + 2];
                for (int i = 1; i < forest.GetLength(0); i++)
                    forest[i, 0] = i;
                for (int j = 1; j < forest.GetLength(1); j++)
                    forest[0, j] = j;
                for (int i = al; i <= ar; i++)
                    for (int j = bl; j <= br; j++)
                    {
                        int x = i - al + 1, y = j - bl + 1;
                        var deletion = forest[x - 1, y] + 1;
                        var insertion = forest[x, y - 1] + 1;
                        int edit = Math.Min(deletion, insertion);
                        if (a[i].Leaf == al && b[j].Leaf == bl)
                        {
                            forest[x, y] = Math.Min(edit, forest[x - 1, y - 1] + (a[i].Kind == b[j].Kind ? 0 : 1));
                            distance[i, j] = forest[x, y];
                        }
                        else
                            forest[x, y] = Math.Min(edit, forest[a[i].Leaf - al, b[j].Leaf - bl] + distance[i, j]);
                    }
            }
        return distance[a.Count - 1, b.Count - 1];
    }
}
