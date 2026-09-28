using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Proscenium.CodeQuality.Deslop;

internal static class CodeQualityPolicy
{
    private const RegexOptions DefaultRegexOptions =
        RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static int s_checksTotal;
    private static int s_checksFailed;
    private static bool s_hasFailures;
    private static bool s_quiet;
    private static string s_repoRoot = string.Empty;

    public static int Main(string[] args)
    {
        if (args is ["--quiet"])
        {
            s_quiet = true;
        }
        else if (args.Length != 0)
        {
            Console.Error.WriteLine($"usage: {GetSourceFilePath()} [--quiet]");
            return 2;
        }

        s_repoRoot = FindRepositoryRoot();

        var allFiles = EnumerateFiles().Order(StringComparer.Ordinal).ToArray();
        if (!s_quiet)
        {
            return RunPolicy(allFiles);
        }

        using var scanLock = AcquireScanLock();
        var fingerprint = ComputeFingerprint(WithExtensions(allFiles,
            ".cs",
            ".csx",
            ".csproj",
            ".props",
            ".targets",
            ".editorconfig",
            ".globalconfig",
            ".ruleset",
            ".wasm",
            ".sh",
            ".ps1",
            ".cmd",
            ".bat",
            ".yml",
            ".yaml"));
        var cachePath = Path.Combine(s_repoRoot, "eng", "code-quality", "obj", "policy-cache", "success.sha256");
        if (File.Exists(cachePath) &&
            File.ReadAllText(cachePath).Trim().Equals(fingerprint, StringComparison.Ordinal))
        {
            return 0;
        }

        var result = RunPolicy(allFiles);
        if (result == 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllText(cachePath, fingerprint);
        }

        return result;
    }

    private static int RunPolicy(string[] allFiles)
    {
        var csFiles = WithExtensions(allFiles, ".cs", ".csx");
        var projectConfigFiles = WithExtensions(allFiles, ".csproj", ".props", ".targets", ".editorconfig");
        var editorConfigFiles = WithExtensions(allFiles, ".editorconfig", ".globalconfig");
        var projectFiles = WithExtensions(allFiles, ".csproj", ".props", ".targets");
        var scriptCiFiles = WithExtensions(allFiles, ".sh", ".ps1", ".cmd", ".bat", ".yml", ".yaml");
        var policyCarrierFiles = WithExtensions(
            allFiles,
            ".sh",
            ".ps1",
            ".cmd",
            ".bat",
            ".yml",
            ".yaml",
            ".props",
            ".targets",
            ".csproj");
        var unsafeProjectFiles = OutsidePlatformProjects(projectFiles);
        var unsafeCsFiles = OutsidePlatformProjects(csFiles);

        if (!s_quiet)
        {
            Console.WriteLine("==================================================================");
            Console.WriteLine(" CODE-QUALITY POLICY SCAN");
            Console.WriteLine($" repo:   {s_repoRoot}");
            Console.WriteLine($" scope:  {csFiles.Length} source files + {projectConfigFiles.Length} config/project files");
            Console.WriteLine("         (build/, build-common/, external/ and build outputs excluded)");
            Console.WriteLine("==================================================================");
        }

        RunRegexCheck(
            "no '#pragma warning disable'",
            @"^\s*#pragma\s+warning\s+disable(?:\s|$)",
            csFiles);

        RunRegexCheck(
            "no suppression/bypass attributes (SuppressMessage, ExcludeFromCodeCoverage, ...)",
            @"\[[^\]]*(?:Suppress[\p{L}\p{N}_]*|ExcludeFromCodeCoverage|GeneratedCode|DebuggerNonUserCode)(?:Attribute)?(?:[^\p{L}\p{N}_]|$)",
            csFiles);

        RunRegexCheck(
            "no CA15xx severity downgrades in editorconfig",
            @"^\s*dotnet_diagnostic\.CA15\d{2}\.severity\s*=\s*(?:none|silent|suggestion|warning)(?:\s|$)",
            editorConfigFiles);

        RunRegexCheck(
            "no blanket analyzer severity downgrades",
            @"^\s*dotnet_analyzer_diagnostic\.severity\s*=\s*(?:none|silent|suggestion|warning)(?:\s|$)",
            editorConfigFiles);

        RunRegexCheck(
            "no generated_code=true policy bypasses",
            @"^\s*generated_code\s*=\s*true(?:\s|$)",
            editorConfigFiles);

        RunRegexCheck(
            "no <NoWarn> in project files",
            @"<NoWarn(?:\s|>)",
            projectFiles);

        RunRegexCheck(
            "no <WarningsNotAsErrors> in project files",
            @"<WarningsNotAsErrors(?:\s|>)",
            projectFiles);

        RunRegexCheck(
            "no command-line NoWarn= bypasses in scripts/CI",
            @"(?:^|[^\p{L}\p{N}_])(?:-p:|/p:|--property[ :])NoWarn=",
            scriptCiFiles);

        RunRegexCheck(
            "no <RunAnalyzers>false</RunAnalyzers> in project files",
            @"<RunAnalyzers(?:DuringBuild|DuringLiveAnalysis)?(?:\s[^>]*)?>\s*false\s*</RunAnalyzers(?:DuringBuild|DuringLiveAnalysis)?>",
            projectFiles);

        RunRegexCheck(
            "no command-line analyzer-disabling bypasses",
            @"(?:^|[^\p{L}\p{N}_])RunAnalyzers(?:DuringBuild|DuringLiveAnalysis)?\s*=\s*false(?:[^\p{L}\p{N}_]|$)",
            policyCarrierFiles);

        RunRegexCheck(
            "no <CodeAnalysisRuleSet> in project files",
            @"<CodeAnalysisRuleSet(?:\s|>)",
            projectFiles);

        RunRegexCheck(
            "no command-line ruleset-override bypasses",
            @"(?:^|[^\p{L}\p{N}_])CodeAnalysisRuleSet\s*=",
            policyCarrierFiles);

        RunRegexCheck(
            "no AllowUnsafeBlocks=true outside Platform.* projects and the browser host",
            @"<AllowUnsafeBlocks[^>]*>\s*true",
            unsafeProjectFiles,
            RegexOptions.IgnoreCase);

        RunRegexCheck(
            "no 'unsafe' keyword outside Platform.* projects and the browser host",
            @"(?:^|[^\p{L}\p{N}_])un" + @"safe(?:[\s{(]|$)",
            unsafeCsFiles);

        RunOneTypePerFileCheck(csFiles);

        RunListCheck(
            "no C# duplication (minimum 30 AST nodes) or redundant helpers",
            DeslopQualityGate.FindViolations(
                s_repoRoot,
                csFiles.Where(IsAnalyzedCSharpFile)));

        RunListCheck(
            "no GlobalSuppressions.cs / .ruleset files",
            allFiles.Where(
                path => HasFileNameOrExtension(path, "GlobalSuppressions.cs", ".ruleset")));

        RunListCheck(
            "no stray .editorconfig / .globalconfig files",
            allFiles.Where(
                path => !path.Equals(".editorconfig", StringComparison.Ordinal) &&
                        HasFileNameOrExtension(path, ".editorconfig", ".globalconfig")));

        if (s_hasFailures)
        {
            Console.WriteLine("------------------------------------------------------------------");
            Console.WriteLine($" RESULT: FAIL  ({s_checksFailed} of {s_checksTotal} checks found violations)");
            Console.WriteLine("==================================================================");
            return 1;
        }

        if (!s_quiet)
        {
            Console.WriteLine("------------------------------------------------------------------");
            Console.WriteLine($" RESULT: PASS  ({s_checksTotal}/{s_checksTotal} checks clean, {csFiles.Length} files scanned)");
            Console.WriteLine("==================================================================");
        }

        return 0;
    }

    private static bool HasFileNameOrExtension(string path, string fileName, string extension)
        => Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateFiles()
    {
        var pending = new Stack<string>();
        pending.Push(s_repoRoot);

        while (pending.TryPop(out var directory))
        {
            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                if (!ShouldSkipDirectory(childDirectory))
                {
                    pending.Push(childDirectory);
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (!file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                {
                    yield return NormalizeRelativePath(file);
                }
            }
        }
    }

    private static bool ShouldSkipDirectory(string directory)
    {
        var name = Path.GetFileName(directory);
        if (name is ".git" or ".idea" or ".vs" or "bin" or "obj" or "TestResults")
        {
            return true;
        }

        var relative = NormalizeRelativePath(directory);
        return relative is "artifacts" or "build" or "build-common" or "external";
    }

    private static string NormalizeRelativePath(string path) =>
        Path.GetRelativePath(s_repoRoot, path).Replace('\\', '/');

    private static string[] WithExtensions(IEnumerable<string> files, params string[] extensions) =>
        files.Where(
                file => extensions.Any(
                    extension => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

    private static string[] OutsidePlatformProjects(IEnumerable<string> files) =>
        files.Where(
                file => !file.StartsWith("samples/System7.Demo.Browser/", StringComparison.Ordinal) &&
                        (!file.StartsWith("src/", StringComparison.Ordinal) ||
                         !file.Split('/')[1].Contains("Platform", StringComparison.Ordinal)))
            .ToArray();

    private static void RunRegexCheck(
        string title,
        string pattern,
        IEnumerable<string> files,
        RegexOptions additionalOptions = RegexOptions.None)
    {
        s_checksTotal++;
        var regex = new Regex(pattern, DefaultRegexOptions | additionalOptions);
        var violations = new List<string>();

        foreach (var file in files)
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(Path.Combine(s_repoRoot, file)))
            {
                lineNumber++;
                if (regex.IsMatch(line))
                {
                    violations.Add($"{file}:{lineNumber}:{line}");
                }
            }
        }

        ReportCheck(title, violations, includeCount: true);
    }

    private static void RunOneTypePerFileCheck(IEnumerable<string> files)
    {
        s_checksTotal++;
        var typeDeclaration = new Regex(
            @"^(?:(?:public|internal|file|sealed|abstract|static|partial|readonly|ref|unsafe)\s+)*(?:class|struct|record|interface|enum|delegate)\b",
            DefaultRegexOptions);
        var violations = new List<string>();

        foreach (var file in files)
        {
            var count = File.ReadLines(Path.Combine(s_repoRoot, file)).Count(line => typeDeclaration.IsMatch(line));
            if (count > 1)
            {
                violations.Add($"{file} ({count.ToString(CultureInfo.InvariantCulture)} types)");
            }
        }

        ReportCheck("one top-level type per file", violations, includeCount: false);
    }

    private static void RunListCheck(string title, IEnumerable<string> results)
    {
        s_checksTotal++;
        ReportCheck(title, results.ToArray(), includeCount: false);
    }

    private static void ReportCheck(
        string title,
        IReadOnlyCollection<string> violations,
        bool includeCount)
    {
        if (violations.Count == 0)
        {
            if (!s_quiet)
            {
                Console.WriteLine($"  [ ok ] {title}");
            }

            return;
        }

        Console.WriteLine(
            includeCount
                ? $"  [FAIL] {title}  ({violations.Count.ToString(CultureInfo.InvariantCulture)} violation(s))"
                : $"  [FAIL] {title}");
        foreach (var violation in violations)
        {
            Console.WriteLine($"           > {violation}");
        }

        s_checksFailed++;
        s_hasFailures = true;
    }

    private static Mutex AcquireScanLock()
    {
        var rootHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s_repoRoot)));
        var mutex = new Mutex(initiallyOwned: false, $"CodeQualityPolicy.{rootHash}");
        try
        {
            mutex.WaitOne();
        }
        catch (AbandonedMutexException)
        {
            // The abandoned mutex is acquired by this process.
        }

        return mutex;
    }

    private static string ComputeFingerprint(IEnumerable<string> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file));
            hash.AppendData([0]);
            using var stream = File.OpenRead(Path.Combine(s_repoRoot, file));
            hash.AppendData(SHA256.HashData(stream));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static bool IsAnalyzedCSharpFile(string file)
        => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(GetSourceFilePath())!);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) &&
                Directory.Exists(Path.Combine(directory.FullName, "eng", "code-quality")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static string GetSourceFilePath([CallerFilePath] string path = "") => path;
}
