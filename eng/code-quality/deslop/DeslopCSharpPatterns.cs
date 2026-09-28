using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Proscenium.CodeQuality.Deslop;

// Classify syntax, not application names. Only complete, simple ranges can be
// discarded; control flow and calculations stay visible.
internal static class DeslopCSharpPatterns
{
    internal sealed record Pattern(string? Identity);
    private static readonly ConditionalWeakTable<IReadOnlyList<DeslopFile>, ConcurrentDictionary<(DeslopFile, int, int), Pattern>> Cache = new();

    internal static Pattern Classify(DeslopFingerprint member, IReadOnlyList<DeslopFile> files)
    {
        var file = files[member.FileId];
        return Cache.GetOrCreateValue(files).GetOrAdd((file, member.Start, member.End), _ => Build(member, file, files));
    }

    private static Pattern Build(DeslopFingerprint member, DeslopFile file, IReadOnlyList<DeslopFile> files)
    {
        if (!DeslopCSharpSyntax.ValidRange(file, member))
            return new(null);
        var root = file.SyntaxTree.GetRoot();
        var span = TextSpan.FromBounds(
            Encoding.UTF8.GetCharCount(file.Source.AsSpan(0, member.Start)),
            Encoding.UTF8.GetCharCount(file.Source.AsSpan(0, member.End)));
        var selected = root.FindNode(span, getInnermostNodeForTie: true);
        if (selected.Span != span || root.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
            return new(null);
        var model = DeslopCSharpSymbols.Model(root, file, files)!;
        if (selected.DescendantNodesAndSelf().Any(node => !Simple(node, model)))
            return new(null);
        var writer = new IdentityWriter(model);
        writer.Write(selected);
        return new(writer.Result.ToString());
    }

    private static bool Simple(SyntaxNode node, SemanticModel model)
    {
        if (node is StatementSyntax and not (BlockSyntax or ExpressionStatementSyntax
            or ReturnStatementSyntax or LocalDeclarationStatementSyntax or EmptyStatementSyntax))
            return false;
        if (node is BinaryExpressionSyntax or PrefixUnaryExpressionSyntax
            or ConditionalExpressionSyntax or IsPatternExpressionSyntax or QueryExpressionSyntax or SwitchStatementSyntax)
            return node is ExpressionSyntax expression && model.GetConstantValue(expression).HasValue;
        if (node is PostfixUnaryExpressionSyntax postfix && !postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression)
            || node is AssignmentExpressionSyntax assignment && !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            || node is SwitchExpressionArmSyntax { WhenClause: not null })
            return false;
        return true;
    }

    private sealed class IdentityWriter(SemanticModel model)
    {
        private readonly Dictionary<ISymbol, int> names = new(SymbolEqualityComparer.Default);
        internal readonly StringBuilder Result = new();
        private void Token(SyntaxToken token)
        {
            var text = token.ValueText;
            text = DeslopCSharpSymbols.InputName(token, model, names, includeType: true) ?? text;
            Result.Append(token.RawKind).Append(':').Append(text.Length).Append(':').Append(text).Append(';');
        }
        internal void Write(SyntaxNode node)
        {
            switch (node)
            {
                case ArrowExpressionClauseSyntax arrow:
                    Write(arrow.Expression);
                    return;
                case BlockSyntax block:
                    foreach (var statement in block.Statements)
                        Write(statement);
                    return;
                case ReturnStatementSyntax { Expression: { } value }:
                    Write(value);
                    return;
                case ExpressionStatementSyntax statement:
                    Write(statement.Expression);
                    return;
            }
            foreach (var token in node.DescendantTokens())
                Token(token);
            Result.Append('|');
        }
    }
}
