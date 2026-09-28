using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Proscenium.CodeQuality.Deslop;

internal static class RedundantFlowAnalyzer
{
    internal static IEnumerable<string> FindViolations(IReadOnlyList<DeslopFile> files, IReadOnlySet<SyntaxTree> visibleTrees)
    {
        foreach (var file in files.Where(file => !file.Skipped && visibleTrees.Contains(file.SyntaxTree)))
        {
            var root = file.SyntaxTree.GetRoot();
            var model = DeslopCSharpSymbols.Model(root, file, files)!;
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(method) is not IMethodSymbol symbol)
                    continue;
                if (DeslopCSharpSymbols.IsInternalImplementation(symbol))
                {
                    if (HasFallbackParameters(method, model))
                        yield return Diagnostic("PSCQ004", method, "Move the fallback value and its mode flag to the caller. Preserve missing-value behavior.");
                    if (HasRedundantLimit(method, model))
                        yield return Diagnostic("PSCQ006", method, "Replace the boolean stop flag and numeric limit with one effective limit.");
                }
                foreach (var variable in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    if (variable.Initializer?.Value is not BaseObjectCreationExpressionSyntax
                        || model.GetDeclaredSymbol(variable) is not ILocalSymbol local || !IsList(local.Type))
                        continue;
                    var selectedReads = 0;
                    if (HasOnlyScalarUses(method, model, local, true, new(SymbolEqualityComparer.Default), ref selectedReads)
                        && selectedReads == 1)
                        yield return Diagnostic("PSCQ005", variable, "This temporary list only supplies one result. Track the count and selected value directly.");
                }
                foreach (var choice in method.DescendantNodes().OfType<BinaryExpressionSyntax>()
                             .Where(expression => expression.IsKind(SyntaxKind.LogicalOrExpression)))
                {
                    if (RepeatsAliasComparison(choice, model))
                        yield return Diagnostic("PSCQ007", choice, "Both comparisons read aliases of the same getter. Use one getter for this comparison.");
                }
            }
        }
    }

    private static bool HasFallbackParameters(MethodDeclarationSyntax method, SemanticModel model)
    {
        var expression = method.ExpressionBody?.Expression
            ?? (method.Body?.Statements.LastOrDefault() as ReturnStatementSyntax)?.Expression;
        if (expression is null || Unwrap(expression) is not ConditionalExpressionSyntax conditional)
            return false;
        var condition = Unwrap(conditional.Condition);
        var whenTrue = conditional.WhenTrue;
        var whenFalse = conditional.WhenFalse;
        while (condition is PrefixUnaryExpressionSyntax negation && negation.IsKind(SyntaxKind.LogicalNotExpression))
        {
            condition = Unwrap(negation.Operand);
            (whenTrue, whenFalse) = (whenFalse, whenTrue);
        }
        if (model.GetSymbolInfo(condition).Symbol is not IParameterSymbol
            { Type.SpecialType: SpecialType.System_Boolean, RefKind: RefKind.None } flag
            || References(method, model, flag).Count() != 1)
            return false;
        return new[] { whenTrue, whenFalse }.Any(branch =>
            model.GetSymbolInfo(Unwrap(branch)).Symbol is IParameterSymbol { RefKind: RefKind.None } fallback
            && !SymbolEqualityComparer.Default.Equals(flag, fallback)
            && References(method, model, fallback).Count() == 1);
    }

    private static bool HasRedundantLimit(MethodDeclarationSyntax method, SemanticModel model)
    {
        foreach (var choice in method.DescendantNodes().OfType<BinaryExpressionSyntax>()
                     .Where(expression => expression.IsKind(SyntaxKind.LogicalOrExpression)))
        {
            var firstOperand = Unwrap(choice.Left);
            var limitOperand = Unwrap(choice.Right);
            if (limitOperand.IsKind(SyntaxKind.LogicalAndExpression))
                (firstOperand, limitOperand) = (limitOperand, firstOperand);
            if (firstOperand is not BinaryExpressionSyntax first || !first.IsKind(SyntaxKind.LogicalAndExpression)
                || limitOperand is not BinaryExpressionSyntax limitCount)
                continue;
            var flagOperand = Unwrap(first.Left);
            var countOperand = Unwrap(first.Right);
            if (flagOperand is BinaryExpressionSyntax)
                (flagOperand, countOperand) = (countOperand, flagOperand);
            if (model.GetSymbolInfo(flagOperand).Symbol is not IParameterSymbol
                { Type.SpecialType: SpecialType.System_Boolean, RefKind: RefKind.None } flag
                || countOperand is not BinaryExpressionSyntax firstCount
                || !IsLowerBound(firstCount) || !IsLowerBound(limitCount)
                || !model.GetConstantValue(firstCount.Right).HasValue
                || model.GetSymbolInfo(Unwrap(limitCount.Right)).Symbol is not IParameterSymbol
                { Type.SpecialType: SpecialType.System_Int32 or SpecialType.System_Int64, RefKind: RefKind.None } limit
                || model.GetSymbolInfo(Unwrap(firstCount.Left)).Symbol is not { } count
                || count is not (ILocalSymbol or IParameterSymbol { RefKind: RefKind.None })
                || model.GetTypeInfo(firstCount.Left).Type?.SpecialType != limit.Type.SpecialType
                || !SymbolEqualityComparer.Default.Equals(count, model.GetSymbolInfo(Unwrap(limitCount.Left)).Symbol)
                || References(method, model, flag).Count() != 1 || References(method, model, limit).Count() != 1)
                continue;
            return true;
        }
        return false;
    }

    private static bool IsLowerBound(BinaryExpressionSyntax expression)
        => expression.IsKind(SyntaxKind.GreaterThanExpression) || expression.IsKind(SyntaxKind.GreaterThanOrEqualExpression);

    private static bool HasOnlyScalarUses(
        SyntaxNode scope,
        SemanticModel model,
        ISymbol collection,
        bool selectsResult,
        HashSet<ISymbol> visited,
        ref int selectedReads)
    {
        if (!visited.Add(collection))
            return true;
        foreach (var reference in References(scope, model, collection))
        {
            if (reference.Ancestors().TakeWhile(node => node != scope)
                .Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
                return false;
            if (reference.Parent is MemberAccessExpressionSyntax member && member.Expression == reference)
            {
                if (member.Name.Identifier.ValueText == "Count")
                    continue;
                if (member.Parent is InvocationExpressionSyntax && member.Name.Identifier.ValueText is "Add" or "Clear")
                    continue;
                if (member.Name.Identifier.ValueText == "RemoveRange"
                    && model.GetOperation(member.Parent!) is IInvocationOperation { Arguments: [var start, var count] }
                    && start.Value.ConstantValue is { HasValue: true, Value: 0 }
                    && count.Value is IPropertyReferenceOperation { Property.Name: "Count", Instance: { } instance }
                    && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(instance.Syntax).Symbol, collection))
                    continue;
                return false;
            }
            if (reference.Parent is ElementAccessExpressionSyntax element && element.Expression == reference)
            {
                if (!selectsResult || !IsBoundaryIndex(element, model, collection) || !element.Ancestors().TakeWhile(node => node != scope).OfType<ReturnStatementSyntax>().Any())
                    return false;
                selectedReads++;
                continue;
            }
            if (reference.Parent is not ArgumentSyntax argument || argument.RefKindKeyword.Kind() != SyntaxKind.None
                || argument.Parent?.Parent is not InvocationExpressionSyntax call
                || model.GetSymbolInfo(call).Symbol is not IMethodSymbol called
                || !DeslopCSharpSymbols.IsInternalImplementation(called)
                || called.OriginalDefinition.DeclaringSyntaxReferences is not [var declaration]
                || declaration.GetSyntax() is not MethodDeclarationSyntax helper)
                return false;
            var ordinal = argument.NameColon is { } named
                ? called.Parameters.FirstOrDefault(parameter => parameter.Name == named.Name.Identifier.ValueText)?.Ordinal ?? -1
                : call.ArgumentList.Arguments.IndexOf(argument);
            if (ordinal < 0 || ordinal >= called.Parameters.Length || !IsList(called.Parameters[ordinal].Type))
                return false;
            var helperModel = model.Compilation.GetSemanticModel(helper.SyntaxTree);
            if (helperModel.GetDeclaredSymbol(helper.ParameterList.Parameters[ordinal]) is not IParameterSymbol parameter
                || !HasOnlyScalarUses(helper, helperModel, parameter, false, visited, ref selectedReads))
                return false;
        }
        return true;
    }

    private static bool IsBoundaryIndex(ElementAccessExpressionSyntax element, SemanticModel model, ISymbol collection)
    {
        if (element.ArgumentList.Arguments is not [var argument])
            return false;
        var index = Unwrap(argument.Expression);
        if (model.GetConstantValue(index) is { HasValue: true, Value: 0 })
            return true;
        if (index is PrefixUnaryExpressionSyntax fromEnd && fromEnd.IsKind(SyntaxKind.IndexExpression))
            return model.GetConstantValue(fromEnd.Operand) is { HasValue: true, Value: 1 };
        return index is BinaryExpressionSyntax subtraction && subtraction.IsKind(SyntaxKind.SubtractExpression)
            && model.GetConstantValue(subtraction.Right) is { HasValue: true, Value: 1 }
            && Unwrap(subtraction.Left) is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Count" } count
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(count.Expression).Symbol, collection);
    }

    private static bool RepeatsAliasComparison(BinaryExpressionSyntax choice, SemanticModel model)
    {
        if (Unwrap(choice.Left) is not BinaryExpressionSyntax left || !left.IsKind(SyntaxKind.EqualsExpression)
            || Unwrap(choice.Right) is not BinaryExpressionSyntax right || !right.IsKind(SyntaxKind.EqualsExpression)
            || model.GetOperation(left) is not IBinaryOperation { OperatorMethod: null }
            || model.GetOperation(right) is not IBinaryOperation { OperatorMethod: null }
            || model.GetOperation(choice)?.DescendantsAndSelf().Any(operation =>
                operation is IConversionOperation { OperatorMethod: not null }) == true
            || !SyntaxFactory.AreEquivalent(left.Right, right.Right)
            || (Unwrap(left.Right) is not LiteralExpressionSyntax
                && model.GetSymbolInfo(Unwrap(left.Right)).Symbol is not (ILocalSymbol or IParameterSymbol { RefKind: RefKind.None }))
            || ReadCall(left.Left) is not { } first || ReadCall(right.Left) is not { } second
            || model.GetSymbolInfo(first).Symbol is not IMethodSymbol firstMethod
            || model.GetSymbolInfo(second).Symbol is not IMethodSymbol secondMethod
            || SymbolEqualityComparer.Default.Equals(firstMethod, secondMethod)
            || Receiver(first, firstMethod, model) is not { } receiver
            || !SymbolEqualityComparer.Default.Equals(receiver, Receiver(second, secondMethod, model)))
            return false;
        var target = CanonicalGetter(firstMethod, model.Compilation);
        return target is not null && SymbolEqualityComparer.Default.Equals(target, CanonicalGetter(secondMethod, model.Compilation));
    }

    private static ISymbol? Receiver(InvocationExpressionSyntax call, IMethodSymbol method, SemanticModel model)
        => call.Expression is MemberAccessExpressionSyntax member && member.Expression is not ThisExpressionSyntax
            ? model.GetSymbolInfo(member.Expression).Symbol switch
            {
                ILocalSymbol { RefKind: RefKind.None } local => local,
                IParameterSymbol { RefKind: RefKind.None } parameter => parameter,
                _ => null,
            }
            : method.ContainingType;

    private static IMethodSymbol? CanonicalGetter(IMethodSymbol method, Compilation compilation)
    {
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        while (visited.Add(method))
        {
            if (method.IsStatic || method.Parameters.Length != 0 || method.IsAbstract || method.IsAsync
                || method.ReturnsByRef || method.ReturnsByRefReadonly
                || ((method.IsVirtual || method.IsOverride) && !method.IsSealed && !method.ContainingType.IsSealed)
                || method.DeclaringSyntaxReferences is not [var source]
                || source.GetSyntax() is not MethodDeclarationSyntax declaration)
                return null;
            var expression = declaration.ExpressionBody?.Expression
                ?? (declaration.Body?.Statements is [ReturnStatementSyntax statement] ? statement.Expression : null);
            var model = compilation.GetSemanticModel(declaration.SyntaxTree);
            if (expression is null)
                return null;
            expression = Unwrap(expression);
            if (model.GetDeclaredSymbol(declaration) is not IMethodSymbol declared
                || model.ClassifyConversion(expression, declared.ReturnType).IsUserDefined)
                return null;
            if (model.GetOperation(expression) is { ConstantValue.HasValue: true } or IDefaultValueOperation
                or IFieldReferenceOperation { Field.IsReadOnly: true, Field.IsVolatile: false, Instance: IInstanceReferenceOperation or null })
                return method;
            if (expression is not InvocationExpressionSyntax call
                || call.Expression is not (SimpleNameSyntax or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax })
                || model.GetSymbolInfo(call).Symbol is not IMethodSymbol target
                || !SymbolEqualityComparer.Default.Equals(method.ReturnType, target.ReturnType)
                || !SymbolEqualityComparer.Default.Equals(method, method.OriginalDefinition))
                return null;
            method = target;
        }
        return null;
    }

    private static InvocationExpressionSyntax? ReadCall(ExpressionSyntax expression)
        => Unwrap(expression) is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } call ? call : null;

    private static IEnumerable<IdentifierNameSyntax> References(SyntaxNode scope, SemanticModel model, ISymbol symbol)
        => scope.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(name => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, symbol));

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parentheses)
            expression = parentheses.Expression;
        return expression;
    }

    private static bool IsList(ITypeSymbol type)
        => type is INamedTypeSymbol { Name: "List", Arity: 1 }
            && type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic";

    private static string Diagnostic(string rule, SyntaxNode node, string message)
        => $"{rule}: {DeslopCSharpSymbols.Location(node)}: {message}";
}
