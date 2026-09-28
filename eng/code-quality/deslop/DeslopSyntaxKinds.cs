using System.Collections.Concurrent;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Proscenium.CodeQuality.Deslop;

internal static class DeslopSyntaxKinds
{
    private static readonly ConcurrentDictionary<SyntaxKind, string> Names = new();

    internal static string Node(SyntaxNode node) => node switch
    {
        IdentifierNameSyntax => "identifier",
        BinaryExpressionSyntax => "binary_expression",
        AssignmentExpressionSyntax => "assignment_expression",
        PrefixUnaryExpressionSyntax => "prefix_unary_expression",
        PostfixUnaryExpressionSyntax => "postfix_unary_expression",
        MemberAccessExpressionSyntax => "member_access_expression",
        FileScopedNamespaceDeclarationSyntax => "namespace_declaration",
        LiteralExpressionSyntax literal => literal.Kind() switch
        {
            SyntaxKind.StringLiteralExpression or SyntaxKind.Utf8StringLiteralExpression => "string_literal",
            SyntaxKind.CharacterLiteralExpression => "character_literal",
            SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression => "boolean_literal",
            SyntaxKind.NullLiteralExpression => "null_literal",
            _ => "numeric_literal",
        },
        _ => Names.GetOrAdd(node.Kind(), kind => SnakeCase(kind.ToString())),
    };

    internal static string? Token(SyntaxToken token)
    {
        if (token.IsKind(SyntaxKind.IdentifierToken))
            return "identifier";
        bool isOperator = token.Parent switch
        {
            BinaryExpressionSyntax binary => token == binary.OperatorToken,
            AssignmentExpressionSyntax assignment => token == assignment.OperatorToken,
            PrefixUnaryExpressionSyntax prefix => token == prefix.OperatorToken,
            PostfixUnaryExpressionSyntax postfix => token == postfix.OperatorToken,
            RangeExpressionSyntax range => token == range.OperatorToken,
            _ => false,
        };
        if (isOperator)
            return "__op__" + token.Text;
        // Syntax nodes already carry statement keywords and generic punctuation.
        // Modifiers are separate named nodes, as they can alter a declaration.
        return SyntaxFacts.IsKeywordKind(token.Kind()) && token.Parent is MemberDeclarationSyntax or ParameterSyntax
            && token.Kind() is not (SyntaxKind.ClassKeyword or SyntaxKind.StructKeyword or SyntaxKind.InterfaceKeyword
                or SyntaxKind.RecordKeyword or SyntaxKind.EnumKeyword or SyntaxKind.DelegateKeyword)
            ? "modifier" : null;
    }

    private static string SnakeCase(string name)
    {
        var result = new StringBuilder();
        foreach (var character in name)
        {
            if (char.IsUpper(character) && result.Length > 0)
                result.Append('_');
            result.Append(char.ToLowerInvariant(character));
        }
        return result.ToString();
    }
}
