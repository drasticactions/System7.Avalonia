using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Proscenium.CodeQuality.Deslop;

internal static class DeslopCSharpSymbols
{
    private static readonly MetadataReference[] FrameworkReferences =
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];
    private static readonly ConditionalWeakTable<IReadOnlyList<DeslopFile>, Lazy<CompilationContext>> Compilations = new();

    internal static string? InputName(SyntaxToken token, SemanticModel model, Dictionary<ISymbol, int> slots, bool includeType = false)
    {
        // nameof observes the spelling, rather than the value of the local.
        if (token.Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().Any(call =>
            call.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }) == true)
            return null;
        ISymbol? symbol = token.Parent switch
        {
            ParameterSyntax parameter when token == parameter.Identifier => model.GetDeclaredSymbol(parameter),
            VariableDeclaratorSyntax variable when token == variable.Identifier => model.GetDeclaredSymbol(variable),
            SingleVariableDesignationSyntax variable => model.GetDeclaredSymbol(variable),
            ForEachStatementSyntax loop when token == loop.Identifier => model.GetDeclaredSymbol(loop),
            CatchDeclarationSyntax exception when token == exception.Identifier => model.GetDeclaredSymbol(exception),
            IdentifierNameSyntax identifier => model.GetSymbolInfo(identifier).Symbol,
            _ => null,
        };
        if (symbol is not ILocalSymbol && symbol is not IParameterSymbol)
            return null;
        var scope = symbol is IParameterSymbol ? symbol.ContainingSymbol : symbol;
        if (!slots.TryGetValue(scope, out var index))
            slots.Add(scope, index = slots.Keys.Count(key => key.Kind == scope.Kind));
        var name = symbol is IParameterSymbol input ? "$parameter:" + index + ":" + input.Ordinal : "$local:" + index;
        var type = symbol is IParameterSymbol parameterType ? parameterType.Type : ((ILocalSymbol)symbol).Type;
        return includeType ? name + ":" + type.ToDisplayString() : name;
    }

    internal static SemanticModel? Model(SyntaxNode node, DeslopFile file, IReadOnlyList<DeslopFile> files)
    {
        if (file.Skipped || !ReferenceEquals(node.SyntaxTree, file.SyntaxTree)
            || !files.Any(candidate => ReferenceEquals(candidate, file)))
            return null;
        var context = Compilations.GetValue(files,
            input => new Lazy<CompilationContext>(() => new CompilationContext(input))).Value;
        return context.Model(file.SyntaxTree);
    }

    internal static bool IsInternalImplementation(IMethodSymbol method)
        => method.DeclaredAccessibility is Accessibility.Private or Accessibility.Internal or Accessibility.ProtectedAndInternal
            && !method.IsVirtual && !method.IsOverride && !method.IsAbstract && !method.IsExtern
            && method.GetAttributes().Length == 0;

    internal static string Location(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return $"{span.Path}:{span.StartLinePosition.Line + 1}";
    }

    private sealed class CompilationContext(IReadOnlyList<DeslopFile> files)
    {
        private readonly CSharpCompilation _compilation = CSharpCompilation.Create("DeslopSymbols",
            files.Where(file => !file.Skipped).Select(file => file.SyntaxTree).Distinct(),
            FrameworkReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        private readonly ConcurrentDictionary<SyntaxTree, SemanticModel> _models = new();

        internal SemanticModel Model(SyntaxTree tree)
            => _models.GetOrAdd(tree, syntaxTree => _compilation.GetSemanticModel(syntaxTree));
    }
}
