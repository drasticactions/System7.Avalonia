namespace Proscenium.CodeQuality.Deslop;

public static class DeslopQualityGate
{
    public static IReadOnlyList<string> FindViolations(string repositoryRoot, IEnumerable<string> sourcePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(sourcePaths);
        var files = ParseFiles(repositoryRoot, sourcePaths);
        var analysis = DeslopEngine.Analyze(files, 30);
        var report = DeslopReport.Build(analysis, files, repositoryRoot, 30, 0);
        var violations = new List<string>();
        foreach (var skipped in report["skipped_files"]!.AsArray())
        {
            violations.Add($"{skipped!["path"]!.GetValue<string>()}: duplication analysis skipped: {skipped["reason"]!.GetValue<string>()}");
        }
        foreach (var cluster in report["clusters"]!.AsArray())
        {
            var locations = cluster!["occurrences"]!.AsArray()
                .Where(occurrence => !occurrence!["hidden"]!.GetValue<bool>())
                .Select(occurrence => $"{occurrence!["path"]!.GetValue<string>()}:{occurrence["start_line"]!.GetValue<int>()}");
            violations.Add($"{cluster["id"]!.GetValue<string>()}: {string.Join("; ", locations)}");
        }

        var visibleTrees = files.Where(file => !DeslopVisibility.IsHidden(file, repositoryRoot))
            .Select(file => file.SyntaxTree).ToHashSet();
        violations.AddRange(RedundantHelperAnalyzer.FindViolations(files, visibleTrees));
        violations.AddRange(RedundantFlowAnalyzer.FindViolations(files, visibleTrees));
        return violations;
    }

    internal static List<DeslopFile> ParseFiles(string root, IEnumerable<string> paths)
    {
        var files = new List<DeslopFile>();
        foreach (var path in paths.Select(path => path.Replace('\\', '/'))
                     .Distinct(StringComparer.Ordinal).Order(DeslopPathComparer.Instance))
        {
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"The Deslop C# parser requires a .cs file: {path}", nameof(paths));
            }

            var source = File.ReadAllBytes(Path.Combine(root, path));
            try
            {
                files.Add(DeslopParser.Parse(path, source, files.Count));
            }
            catch (DeslopAstTooDeepException)
            {
                files.Add(new DeslopFile
                {
                    Path = path,
                    Source = source,
                    SyntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(string.Empty),
                    Skipped = true,
                    Root = new DeslopNode
                    {
                        Kind = "__file__",
                        Start = 0,
                        End = 0,
                        FileId = files.Count,
                    },
                });
            }
        }

        return files;
    }
}
