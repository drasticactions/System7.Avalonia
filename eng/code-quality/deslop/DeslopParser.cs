using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Proscenium.CodeQuality.Deslop;

internal static class DeslopParser
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static DeslopFile Parse(string path, byte[] source, int fileId)
    {
        var text = Utf8.GetString(source);
        var tree = CSharpSyntaxTree.ParseText(text,
            new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.None),
            path);
        var offsets = ByteOffsets(text);
        return new DeslopFile
        {
            Path = path,
            Source = source,
            SyntaxTree = tree,
            Root = Build(tree.GetRoot(), fileId, offsets, 0),
        };
    }

    private static int[] ByteOffsets(string text)
    {
        var offsets = new int[text.Length + 1];
        for (var index = 0; index < text.Length;)
        {
            int width = char.IsHighSurrogate(text[index]) && index + 1 < text.Length
                && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
            if (width == 2)
                offsets[index + 1] = offsets[index];
            offsets[index + width] = offsets[index] + Utf8.GetByteCount(text.AsSpan(index, width));
            index += width;
        }
        return offsets;
    }

    private static DeslopNode Build(SyntaxNode syntax, int fileId, int[] offsets, int depth)
    {
        if (depth > 500)
            throw new DeslopAstTooDeepException();
        var rawKind = DeslopSyntaxKinds.Node(syntax);
        var node = new DeslopNode
        {
            Kind = syntax is CompilationUnitSyntax ? "__file__"
                : syntax is IdentifierNameSyntax or PredefinedTypeSyntax ? "__ident__"
                : syntax is LiteralExpressionSyntax or InterpolatedStringTextSyntax ? "__literal__" : rawKind,
            FileId = fileId,
            Syntax = syntax,
            Start = offsets[syntax.SpanStart],
            End = offsets[syntax.Span.End],
        };
        // These syntax nodes already represent one complete content leaf.
        if (syntax is IdentifierNameSyntax or PredefinedTypeSyntax or LiteralExpressionSyntax
            or InterpolatedStringTextSyntax)
            return node;
        foreach (var item in syntax.ChildNodesAndTokens())
        {
            DeslopNode child;
            if (item.AsNode() is { } nested)
                child = Build(nested is EqualsValueClauseSyntax initializer ? initializer.Value : nested,
                    fileId,
                    offsets,
                    depth + 1);
            else
            {
                var token = item.AsToken();
                if (token.IsMissing || DeslopSyntaxKinds.Token(token) is not { } kind)
                    continue;
                child = new DeslopNode
                {
                    Kind = kind == "identifier" ? "__ident__" : kind,
                    FileId = fileId,
                    Start = offsets[token.SpanStart],
                    End = offsets[token.Span.End],
                };
            }
            child.Parent = node;
            node.Children.Add(child);
        }
        return node;
    }

    public static string Dump(DeslopNode root)
    {
        var result = new StringBuilder();
        void Visit(DeslopNode node, int depth)
        {
            result.Append(' ', depth * 2).Append(node.Kind).Append(" [").Append(node.Start)
                .Append("..").Append(node.End).Append("]\n");
            foreach (var child in node.Children)
                Visit(child, depth + 1);
        }
        Visit(root, 0);
        return result.ToString();
    }
}
