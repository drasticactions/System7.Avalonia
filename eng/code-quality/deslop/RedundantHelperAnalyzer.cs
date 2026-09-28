using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Proscenium.CodeQuality.Deslop;

internal static class RedundantHelperAnalyzer
{
    internal static IEnumerable<string> FindViolations(IReadOnlyList<DeslopFile> files, IReadOnlySet<SyntaxTree> visibleTrees)
    {
        var methods = new List<(IMethodSymbol Symbol, MethodDeclarationSyntax Declaration, SemanticModel Model)>();
        var references = new Dictionary<ISymbol, List<(SyntaxNode Node, INamedTypeSymbol? Caller)>>(SymbolEqualityComparer.Default);
        foreach (var file in files.Where(file => !file.Skipped))
        {
            var root = file.SyntaxTree.GetRoot();
            var model = DeslopCSharpSymbols.Model(root, file, files)!;
            foreach (var declaration in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                if (model.GetDeclaredSymbol(declaration) is IMethodSymbol symbol)
                    methods.Add((symbol, declaration, model));
            CollectReferences(root, model, references);
        }

        var helperTypes = FindSingleConsumerTypes(methods, references, visibleTrees).ToHashSet(SymbolEqualityComparer.Default);
        foreach (var type in helperTypes)
        {
            var declaration = type!.DeclaringSyntaxReferences[0].GetSyntax();
            yield return $"PSCQ003: {DeslopCSharpSymbols.Location(declaration)}: '{type.ToDisplayString()}' contains only simple helpers "
                + "used once each by one other type. Move these helpers into that type.";
        }

        foreach (var method in methods)
        {
            if (!visibleTrees.Contains(method.Declaration.SyntaxTree))
                continue;
            if (!helperTypes.Contains(method.Symbol.ContainingType)
                && CanInline(method.Symbol)
                && references.TryGetValue(method.Symbol.OriginalDefinition, out var uses)
                && uses.Count == 1 && IsDirectCall(uses[0].Node)
                && visibleTrees.Contains(uses[0].Node.SyntaxTree)
                && !uses[0].Node.Ancestors().Contains(method.Declaration)
                && SingleExpression(method.Declaration) is InvocationExpressionSyntax call
                && IsSimpleExpression(call, method.Model))
            {
                yield return $"PSCQ002: {DeslopCSharpSymbols.Location(method.Declaration)}: '{method.Symbol.ToDisplayString()}' "
                    + $"only forwards a call and has one caller at {DeslopCSharpSymbols.Location(uses[0].Node)}. Inline the wrapper.";
            }

            if ((!method.Symbol.IsVirtual && !method.Symbol.IsAbstract) || method.Symbol.IsOverride
                || method.Symbol.DeclaredAccessibility is not (Accessibility.Internal or Accessibility.ProtectedAndInternal)
                || (!method.Symbol.IsAbstract && !OnlyForwards(method.Declaration)))
                continue;

            var overrides = methods.Where(candidate => Overrides(candidate.Symbol, method.Symbol)).Take(2).ToArray();
            if (overrides.Length != 1 || !visibleTrees.Contains(overrides[0].Declaration.SyntaxTree)
                || !OnlyForwards(overrides[0].Declaration))
                continue;

            yield return $"PSCQ001: {DeslopCSharpSymbols.Location(method.Declaration)}: '{method.Symbol.ToDisplayString()}' "
                + $"has one override at {DeslopCSharpSymbols.Location(overrides[0].Declaration)} that only forwards a call. "
                + "Remove the redundant hook and put the operation in its owning implementation.";
        }
    }

    private static void CollectReferences(
        SyntaxNode root,
        SemanticModel model,
        Dictionary<ISymbol, List<(SyntaxNode Node, INamedTypeSymbol? Caller)>> references)
    {
        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            var info = model.GetSymbolInfo(name);
            IEnumerable<ISymbol> symbols = info.Symbol is { } resolved ? [resolved] : info.CandidateSymbols;
            foreach (var symbol in symbols.OfType<IMethodSymbol>())
            {
                var key = (symbol.ReducedFrom ?? symbol).OriginalDefinition;
                if (!references.TryGetValue(key, out var uses))
                    references[key] = uses = [];
                uses.Add((name, model.GetEnclosingSymbol(name.SpanStart)?.ContainingType));
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> FindSingleConsumerTypes(
        List<(IMethodSymbol Symbol, MethodDeclarationSyntax Declaration, SemanticModel Model)> methods,
        Dictionary<ISymbol, List<(SyntaxNode Node, INamedTypeSymbol? Caller)>> references,
        IReadOnlySet<SyntaxTree> visibleTrees)
    {
        foreach (var group in methods.GroupBy(method => (ISymbol)method.Symbol.ContainingType, SymbolEqualityComparer.Default))
        {
            if (group.Key is not INamedTypeSymbol { IsStatic: true, Arity: 0 } type
                || type.DeclaredAccessibility is not (Accessibility.Internal or Accessibility.Private)
                || type.GetAttributes().Length != 0 || group.Count() < 2
                || type.DeclaringSyntaxReferences.Any(declaration => !visibleTrees.Contains(declaration.SyntaxTree))
                || type.GetMembers().Any(member => member is not IMethodSymbol { MethodKind: MethodKind.Ordinary }))
                continue;

            INamedTypeSymbol? consumer = null;
            var singleConsumer = true;
            foreach (var method in group)
            {
                if (method.Symbol.IsAsync || method.Symbol.IsExtern || method.Symbol.GetAttributes().Length != 0
                    || !IsSimpleExpression(SingleExpression(method.Declaration), method.Model)
                    || !references.TryGetValue(method.Symbol.OriginalDefinition, out var uses)
                    || uses.Count != 1 || !IsDirectCall(uses[0].Node) || uses[0].Caller is not { } caller
                    || !visibleTrees.Contains(uses[0].Node.SyntaxTree)
                    || SymbolEqualityComparer.Default.Equals(type, caller)
                    || (consumer is not null && !SymbolEqualityComparer.Default.Equals(consumer, caller)))
                {
                    singleConsumer = false;
                    break;
                }
                consumer = caller;
            }
            if (singleConsumer)
                yield return type;
        }
    }

    private static bool CanInline(IMethodSymbol method)
        => DeslopCSharpSymbols.IsInternalImplementation(method)
            && !method.IsAsync && method.ExplicitInterfaceImplementations.Length == 0;

    private static bool IsDirectCall(SyntaxNode reference)
        => reference.Parent is InvocationExpressionSyntax { Expression: var expression } && ReferenceEquals(expression, reference)
            || reference.Parent is MemberAccessExpressionSyntax member && member.Parent is InvocationExpressionSyntax;

    private static bool IsSimpleExpression(ExpressionSyntax? expression, SemanticModel model)
        => expression is not null && !expression.DescendantNodesAndSelf().Any(node => node
            is AnonymousFunctionExpressionSyntax or ConditionalExpressionSyntax or SwitchExpressionSyntax
                or AwaitExpressionSyntax or AssignmentExpressionSyntax or ThrowExpressionSyntax)
            && model.GetOperation(expression)?.DescendantsAndSelf().OfType<IArgumentOperation>().Any(argument =>
                argument.IsImplicit && argument.Parameter?.GetAttributes().Any(attribute =>
                    attribute.AttributeClass is { } type
                    && type.ContainingNamespace.ToDisplayString() == "System.Runtime.CompilerServices"
                    && type.Name is "CallerMemberNameAttribute" or "CallerFilePathAttribute"
                        or "CallerLineNumberAttribute" or "CallerArgumentExpressionAttribute") == true) != true;

    private static bool Overrides(IMethodSymbol candidate, IMethodSymbol method)
    {
        for (var parent = candidate.OverriddenMethod; parent is not null; parent = parent.OverriddenMethod)
            if (SymbolEqualityComparer.Default.Equals(parent.OriginalDefinition, method.OriginalDefinition))
                return true;
        return false;
    }

    private static bool OnlyForwards(MethodDeclarationSyntax method)
        => IsForwardedCall(SingleExpression(method));

    private static ExpressionSyntax? SingleExpression(MethodDeclarationSyntax method)
        => method.ExpressionBody?.Expression ?? method.Body?.Statements switch
        {
            [ReturnStatementSyntax statement] => statement.Expression,
            [ExpressionStatementSyntax statement] => statement.Expression,
            _ => null,
        };

    private static bool IsForwardedCall(ExpressionSyntax? expression)
        => expression switch
        {
            ParenthesizedExpressionSyntax parentheses => IsForwardedCall(parentheses.Expression),
            AwaitExpressionSyntax awaited => IsForwardedCall(awaited.Expression),
            InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ConfigureAwait" } member,
            } call when HasSimpleArguments(call) => IsForwardedCall(member.Expression),
            InvocationExpressionSyntax call => HasSimpleArguments(call),
            _ => false,
        };

    private static bool HasSimpleArguments(InvocationExpressionSyntax call)
        => call.ArgumentList.Arguments.All(argument => argument.Expression
            is IdentifierNameSyntax or LiteralExpressionSyntax or DefaultExpressionSyntax or ThisExpressionSyntax);
}
