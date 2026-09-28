using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Org.BouncyCastle.Crypto.Digests;

namespace Proscenium.CodeQuality.Deslop;

internal static partial class DeslopEngine
{
    // BLAKE3 hashing from the ported fingerprint engine.
    public static byte[] Digest(IEnumerable<byte[]> chunks, int length = 32)
    {
        var digest = new Blake3Digest();
        foreach (var chunk in chunks)
            digest.BlockUpdate(chunk, 0, chunk.Length);
        var output = new byte[length];
        digest.OutputFinal(output, 0, length);
        return output;
    }

    public static bool IsBoilerplate(DeslopNode node) =>
        node.Kind is "using_directive" or "file_scoped_namespace_declaration" ||
        node.Children.Count > 0 && node.Children.All(IsBoilerplate);

    public static List<DeslopFingerprint> Fingerprints(DeslopNode root, int minNodes, bool language = true)
    {
        var output = new List<DeslopFingerprint>();
        var hashes = new Dictionary<DeslopNode, byte[]>();
        var counts = new Dictionary<DeslopNode, int>();
        void Walk(DeslopNode node, bool inherited)
        {
            bool suppressed = inherited || language && IsBoilerplate(node);
            foreach (var child in node.Children)
                Walk(child, suppressed);
            var hash = Digest(new[] { Encoding.UTF8.GetBytes(node.Kind), new byte[1] }.Concat(node.Children.Select(child => hashes[child])));
            hashes[node] = hash;
            var count = 1 + node.Children.Sum(child => counts[child]);
            counts[node] = count;
            if (count >= minNodes && !suppressed
                && (!language || node.Syntax is BlockSyntax
                    || node.Syntax is ExpressionSyntax { Parent: ArrowExpressionClauseSyntax or LambdaExpressionSyntax }))
                output.Add(new(hash, node.FileId, node.Start, node.End, count));
        }
        Walk(root, false);
        return output;
    }

    // Candidates always cover one complete syntax node.
    public static IReadOnlyList<DeslopNode>? RangeNodes(DeslopNode node, int start, int end)
    {
        if (node.Start == start && node.End == end)
            return new[] { node };
        if (!node.Covers(start, end))
            return null;
        foreach (var child in node.Children)
            if (RangeNodes(child, start, end) is { } result)
                return result;
        return null;
    }

}
