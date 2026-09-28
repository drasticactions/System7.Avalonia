namespace Proscenium.CodeQuality.Deslop;

internal sealed class DeslopAstTooDeepException : InvalidOperationException
{
    public DeslopAstTooDeepException()
        : base("The C# AST exceeds the Deslop depth limit of 500.")
    {
    }
}
