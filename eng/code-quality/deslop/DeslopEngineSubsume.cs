namespace Proscenium.CodeQuality.Deslop;

internal static partial class DeslopEngine
{
    // Rust cluster::subsume::{survivor::same_region_survivor,kernel::resolve,kernel::Resolution}.
    private static List<DeslopCluster> Subsume(List<DeslopCluster> clusters)
    {
        var published = new HashSet<DeslopCluster>();
        foreach (var group in clusters.GroupBy(c => string.Join(",", c.Members.Select(m => m.FileId).Distinct().Order())))
        {
            var views = group.ToArray();
            int count = views.Length;
            List<int>[] EmptyAdjacency() => Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
            var beaters = EmptyAdjacency();
            var beaten = EmptyAdjacency();
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                {
                    var a = views[i];
                    var b = views[j];
                    if (!Paired(a, b) || !Paired(b, a))
                        continue;
                    bool second = Encloses(b, a) || !Encloses(a, b) && Rank(a, b) < 0;
                    int winner = second ? j : i, loser = second ? i : j;
                    beaten[winner].Add(loser);
                    beaters[loser].Add(winner);
                }
            var straddled = new bool[count];
            while (true)
            {
                var kept = SelectSurvivors(views, beaters, beaten, straddled);
                bool removed = false;
                for (int i = 0; i < kept.Count; i++)
                    for (int j = i + 1; j < kept.Count; j++)
                    {
                        int ai = kept[i], bi = kept[j];
                        var a = views[ai];
                        var b = views[bi];
                        if (Overlaps(a, b) && Overlaps(b, a) && Enumerable.Range(0, count).Any(k => k != ai && k != bi && Encloses(a, views[k]) && Encloses(b, views[k])))
                        { straddled[ai] = straddled[bi] = true; removed = true; }
                    }
                if (removed)
                    continue;
                foreach (int i in kept)
                    published.Add(views[i]);
                break;
            }
        }
        return clusters.Where(published.Contains).ToList();
    }

    private static List<int> SelectSurvivors(DeslopCluster[] views, List<int>[] beaters, List<int>[] beaten, bool[] straddled)
    {
        var state = straddled.Select(s => s ? 3 : 0).ToArray();
        var live = beaters.Select(list => list.Count(i => !straddled[i])).ToArray();
        var ready = new SortedSet<int>(Enumerable.Range(0, views.Length).Where(i => state[i] == 0 && live[i] == 0));
        var kept = new List<int>();
        while (true)
        {
            int next;
            if (ready.Count > 0)
            { next = ready.Min; ready.Remove(next); }
            else
            {
                var remaining = Enumerable.Range(0, views.Length).Where(i => state[i] == 0).ToArray();
                if (remaining.Length == 0)
                    break;
                next = remaining[0];
                foreach (int i in remaining.Skip(1))
                    if (Rank(views[i], views[next]) >= 0)
                        next = i;
            }
            state[next] = 1;
            kept.Add(next);
            foreach (int rival in beaten[next].Concat(beaters[next]))
            {
                if (state[rival] != 0)
                    continue;
                state[rival] = 2;
                ready.Remove(rival);
                foreach (int freed in beaten[rival])
                { live[freed] = Math.Max(0, live[freed] - 1); if (live[freed] == 0 && state[freed] == 0) ready.Add(freed); }
            }
        }
        kept.Sort();
        return kept;
    }

    private static bool Paired(DeslopCluster a, DeslopCluster b) => a.Members.Count > 0 && a.Members.All(m => b.Members.Any(n => Covers(m, n) || Covers(n, m)));
    private static bool Encloses(DeslopCluster a, DeslopCluster b) => a.Members.Count > 0 && b.Members.Count > 0 && b.Members.All(m => a.Members.Any(n => Covers(n, m))) && a.Members.Any(m => !b.Members.Any(n => Covers(n, m)));
    private static bool Overlaps(DeslopCluster a, DeslopCluster b) => a.Members.Count > 0 && a.Members.All(m => b.Members.Any(n => m.FileId == n.FileId && Intersects(m, n)));

    private static int Rank(DeslopCluster a, DeslopCluster b)
    {
        int c = a.Members.Count.CompareTo(b.Members.Count);
        if (c == 0)
            c = a.Mass.CompareTo(b.Mass);
        return c != 0 ? c : StringComparer.Ordinal.Compare(b.Id, a.Id);
    }
}
