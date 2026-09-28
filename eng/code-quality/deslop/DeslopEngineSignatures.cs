using System.Buffers.Binary;
using System.Text;

namespace Proscenium.CodeQuality.Deslop;

internal static partial class DeslopEngine
{
    private sealed record TokenState(int Count, string[] Prefix, string[] Suffix, ulong[] Slots);
    private sealed record SignatureOwner(DeslopNode Node, int First, int Count);

    // Original pipeline::signatures::signatures_for_file, ARM64 0x10049f71c.
    // Corrected range ownership follows the top-down resolver before the fast fold.
    // An enclosing exact node or window takes precedence over descendant aliases.
    public static ulong[][] Signatures(DeslopNode root, IReadOnlyList<DeslopFingerprint> fingerprints)
    {
        var output = fingerprints.Select(FallbackSignature).ToArray();
        var positions = Enumerable.Range(0, fingerprints.Count).GroupBy(i => (fingerprints[i].Start, fingerprints[i].End)).ToDictionary(g => g.Key, g => g.ToArray());
        var requested = positions.Keys.GroupBy(range => range.Start).ToDictionary(group => group.Key, group => group.Select(range => range.End).ToHashSet());
        var owners = new Dictionary<(int, int), SignatureOwner>();
        void Reserve(DeslopNode node)
        {
            if (positions.ContainsKey((node.Start, node.End)))
                owners.TryAdd((node.Start, node.End), new(node, -1, 0));
            var starts = new HashSet<int>();
            for (int first = 0; first < node.Children.Count; first++)
            {
                int start = node.Children[first].Start;
                if (!starts.Add(start) || !requested.TryGetValue(start, out var ends))
                    continue;
                int limit = ends.Max(), maximum = int.MinValue;
                for (int last = first; last < node.Children.Count; last++)
                {
                    int end = node.Children[last].End;
                    maximum = Math.Max(maximum, end);
                    if (maximum > limit)
                        break;
                    if (maximum == end && ends.Contains(end) && node.Covers(start, end))
                        owners.TryAdd((start, end), new(node, first, last - first + 1));
                }
            }
            foreach (var child in node.Children)
                Reserve(child);
        }
        Reserve(root);
        var grams = new Dictionary<string, ulong[]>(StringComparer.Ordinal);
        var empty = new TokenState(0, [], [], Enumerable.Repeat(ulong.MaxValue, 128).ToArray());
        void Emit(int start, int end, TokenState state)
        {
            if (state.Count < 5 || !positions.TryGetValue((start, end), out var indexes))
                return;
            foreach (int index in indexes)
                output[index] = state.Slots;
        }
        var windows = owners.Where(pair => pair.Value.First >= 0).GroupBy(pair => pair.Value.Node).ToDictionary(group => group.Key, group => group.ToArray());
        TokenState Walk(DeslopNode node)
        {
            var children = node.Children.Select(Walk).ToArray();
            var state = empty;
            if (!IsBoilerplate(node))
            {
                state = new(1, [node.Kind], [node.Kind], empty.Slots);
                foreach (var child in children)
                    state = JoinTokens(state, child, grams);
            }
            if (owners.TryGetValue((node.Start, node.End), out var exact) && ReferenceEquals(exact.Node, node) && exact.First < 0)
                Emit(node.Start, node.End, state);
            if (windows.TryGetValue(node, out var ownedWindows))
                foreach (var (range, owner) in ownedWindows)
                {
                    var window = empty;
                    for (int index = owner.First; index < owner.First + owner.Count; index++)
                        window = JoinTokens(window, children[index], grams);
                    Emit(range.Item1, range.Item2, window);
                }
            return state;
        }
        Walk(root);
        return output;
    }

    private static TokenState JoinTokens(TokenState left, TokenState right, Dictionary<string, ulong[]> grams)
    {
        if (left.Count == 0)
            return right;
        if (right.Count == 0)
            return left;
        var slots = left.Slots.Zip(right.Slots, Math.Min).ToArray();
        var junction = left.Suffix.Concat(right.Prefix).ToArray();
        for (int start = 0; start + 5 <= junction.Length; start++)
        {
            string key = string.Concat(junction.Skip(start).Take(5).Select(kind => kind + "\0"));
            if (!grams.TryGetValue(key, out var signature))
            {
                signature = DecodeSignature(Digest([Encoding.UTF8.GetBytes(key)], 1024));
                grams.Add(key, signature);
            }
            for (int slot = 0; slot < slots.Length; slot++)
                slots[slot] = Math.Min(slots[slot], signature[slot]);
        }
        return new(left.Count + right.Count, left.Prefix.Concat(right.Prefix).Take(4).ToArray(), left.Suffix.Concat(right.Suffix).TakeLast(4).ToArray(), slots);
    }

    private static ulong[] FallbackSignature(DeslopFingerprint fingerprint)
    {
        var range = new byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(range.AsSpan(0, 8), (ulong)fingerprint.Start);
        BinaryPrimitives.WriteUInt64LittleEndian(range.AsSpan(8, 8), (ulong)fingerprint.End);
        return DecodeSignature(Digest([fingerprint.Hash, range], 1024));
    }

    private static ulong[] DecodeSignature(byte[] bytes) => Enumerable.Range(0, 128).Select(slot => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(slot * 8, 8))).ToArray();
}
