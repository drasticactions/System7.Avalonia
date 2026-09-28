using System.Globalization;
using System.Text.Json;

namespace Proscenium.CodeQuality.Deslop;

internal static class DeslopProgram
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or JsonException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
        {
            Console.Error.WriteLine($"Deslop port: {error.Message}");
            return 2;
        }
    }

    private static int Run(string[] args)
    {
        if (args is ["--debug-ast-list", var listPath])
        {
            var astPaths = ReadPaths(listPath, "The AST file list must contain a JSON array.");
            var results = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var path in astPaths)
            {
                try
                {
                    var parsed = DeslopParser.Parse(path, File.ReadAllBytes(path), 0);
                    results.Add(path, new { ast = DeslopParser.Dump(parsed.Root), error = (string?)null, kind = "ok" });
                }
                catch (Exception error) when (error is IOException or InvalidOperationException or NotSupportedException or ArgumentException)
                {
                    results.Add(path,
                        new
                        {
                            ast = (string?)null,
                            error = error.Message,
                            kind = error is DeslopAstTooDeepException ? "too_deep" : "error"
                        });
                }
            }
            Console.WriteLine(JsonSerializer.Serialize(results));
            return 0;
        }

        if (args is ["--debug-ast", var sourcePath])
        {
            var parsed = DeslopParser.Parse(sourcePath, File.ReadAllBytes(sourcePath), 0);
            Console.Write(DeslopParser.Dump(parsed.Root));
            return 0;
        }

        var options = ParseArguments(args);
        var root = Path.GetFullPath(options.GetValueOrDefault("--root", "."));
        if (!options.TryGetValue("--file-list", out var manifest))
        {
            throw new ArgumentException("Specify --file-list with a JSON array of paths relative to --root.");
        }

        var paths = ReadPaths(manifest, "The file list must contain a JSON array.");
        var minimum = int.Parse(options.GetValueOrDefault("--min-nodes", "30"), CultureInfo.InvariantCulture);
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        double? threshold = options.TryGetValue("--fail-over", out var value)
            ? double.Parse(value, CultureInfo.InvariantCulture) : null;
        if (threshold is { } percent && (!double.IsFinite(percent) || percent is < 0 or > 100))
        {
            throw new ArgumentException("The threshold must be a finite number within [0, 100].");
        }

        var files = DeslopQualityGate.ParseFiles(root, paths);
        var analysis = DeslopEngine.Analyze(files, minimum);
        var report = DeslopReport.Build(analysis, files, root, minimum, threshold);
        var json = report.ToJsonString(s_jsonOptions);
        if (options.TryGetValue("--output", out var output))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, json + Environment.NewLine);
        }
        else
        {
            Console.WriteLine(json);
        }

        return report["metrics"]!["threshold"]!["breached"]!.GetValue<bool>() ? 3 : 0;
    }

    private static string[] ReadPaths(string path, string error)
        => JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? throw new ArgumentException(error);

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var position = 0; position < args.Length; position += 2)
        {
            var key = args[position];
            if (key is not ("--root" or "--file-list" or "--output" or "--min-nodes" or "--fail-over")
                || position + 1 == args.Length || !options.TryAdd(key, args[position + 1]))
            {
                throw new ArgumentException($"Invalid or repeated argument: {key}");
            }
        }

        return options;
    }
}
