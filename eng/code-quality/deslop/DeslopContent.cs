using System.Buffers.Binary;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Org.BouncyCastle.Crypto.Digests;

namespace Proscenium.CodeQuality.Deslop;

// Compare content leaves after normalizing bound local names. Member names,
// operators and literal values remain evidence of different implementations.
internal static class DeslopContent
{
    private static readonly ConditionalWeakTable<DeslopFile, ConcurrentDictionary<(int Start, int End), List<Leaf>>> Frontiers = new();
    private sealed record Leaf(int Population, ulong Key, int? Group = null);

    public static DeslopContentEvidence Measure(DeslopFingerprint left, DeslopFingerprint right, IReadOnlyList<DeslopFile> files)
    {
        var a = Frontier(left, files);
        var b = Frontier(right, files);
        if (a is null || b is null)
            return new(false, 0);
        bool aligned = left.Hash.AsSpan().SequenceEqual(right.Hash) && a.Count == b.Count;
        bool contradicts;
        if (aligned)
            contradicts = a.Zip(b).Any(p => DifferentKeysAtPopulation(p, 2));
        else
        {
            Dictionary<ulong, int> OperatorCounts(IEnumerable<Leaf> leaves)
                => DeslopFrequency.Count(leaves.Where(x => x.Population == 2), x => x.Key);
            var ac = OperatorCounts(a);
            var bc = OperatorCounts(b);
            contradicts = ac.Any(p => p.Value > bc.GetValueOrDefault(p.Key)) && bc.Any(p => p.Value > ac.GetValueOrDefault(p.Key));
        }
        if (contradicts)
            return new(true, 0);
        double agreement;
        if (aligned)
        {
            var pairs = a.Zip(b).Where(p => !(Equal(p.First, p.Second) && p.First.Population == 2)).ToArray();
            agreement = Share(pairs.Count(p => Equal(p.First, p.Second)), pairs.Length);
        }
        else
        {
            var ak = DeslopFrequency.Count(a.Where(leaf => leaf.Population != 2), Key);
            var bk = DeslopFrequency.Count(b.Where(leaf => leaf.Population != 2), Key);
            var keys = ak.Keys.Union(bk.Keys).ToArray();
            agreement = Share(keys.Sum(key => Math.Min(ak.GetValueOrDefault(key), bk.GetValueOrDefault(key))),
                keys.Sum(key => Math.Max(ak.GetValueOrDefault(key), bk.GetValueOrDefault(key))));
        }
        return new(true, agreement);
    }

    private static (int Population, ulong Key, int? Group) Key(Leaf leaf) => (leaf.Population, leaf.Key, leaf.Group);
    private static bool Equal(Leaf a, Leaf b) => Key(a) == Key(b);
    private static double Share(int numerator, int denominator) => denominator == 0 ? 1 : (double)numerator / denominator;

    private static List<Leaf>? Frontier(DeslopFingerprint fp, IReadOnlyList<DeslopFile> files)
    {
        if (fp.FileId < 0 || fp.FileId >= files.Count)
            return null;
        var file = files[fp.FileId];
        var cache = Frontiers.GetValue(file, _ => new());
        if (cache.TryGetValue((fp.Start, fp.End), out var cached))
            return cached;
        var roots = DeslopEngine.RangeNodes(file.Root, fp.Start, fp.End);
        if (roots is null)
            return null;
        var model = DeslopCSharpSymbols.Model(file.SyntaxTree.GetRoot(), file, files)!;
        var locals = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);
        List<Leaf> result = [];
        int group = 0;
        void Walk(DeslopNode node)
        {
            if (DeslopEngine.IsBoilerplate(node))
                return;
            var first = result.Count;
            foreach (var child in node.Children)
                Walk(child);
            bool op = node.Kind.StartsWith("__op__", StringComparison.Ordinal);
            if (result.Count == first && (op || node.Kind is "__ident__" or "__literal__"))
            {
                var bytes = file.Source[node.Start..node.End];
                if (node.Kind == "__ident__")
                {
                    var offset = Encoding.UTF8.GetCharCount(file.Source.AsSpan(0, node.Start));
                    var token = file.SyntaxTree.GetRoot().FindToken(offset);
                    if (DeslopCSharpSymbols.InputName(token, model, locals) is { } name)
                        bytes = Encoding.UTF8.GetBytes(name);
                }
                var hash = new byte[32];
                var digest = new Blake3Digest();
                digest.BlockUpdate(bytes, 0, bytes.Length);
                digest.DoFinal(hash, 0);
                result.Add(new(op ? 2 : node.Kind == "__literal__" ? 1 : 0, BinaryPrimitives.ReadUInt64LittleEndian(hash)));
                return;
            }
            if (node.Kind != "__literal__" || result.Count == first)
                return;
            int id = group++;
            for (int i = first; i < result.Count; i++)
            {
                if (result[i].Population == 1)
                    result[i] = result[i] with { Group = id };
            }
        }
        foreach (var root in roots)
            Walk(root);
        cache.TryAdd((fp.Start, fp.End), result);
        return result;
    }

    private static bool DifferentKeysAtPopulation((Leaf First, Leaf Second) pair, int population)
        => pair.First.Population == population && pair.Second.Population == population
            && pair.First.Key != pair.Second.Key;
}
