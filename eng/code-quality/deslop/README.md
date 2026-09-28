# Managed C# duplication detector

The engineering quality gate uses a managed C# port of the Deslop detection engine. Roslyn supplies syntax trees and semantic classification. BouncyCastle supplies managed BLAKE3 hashing. The detector has no runtime or build dependency on the original Deslop implementation, tree-sitter, or WebAssembly.

The project references Roslyn from the installed .NET SDK. Normal builds require .NET 10 and the BouncyCastle NuGet package. The original Deslop license remains in [LICENSE.Deslop](LICENSE.Deslop).

The gate compares executable blocks and expression bodies with at least 30 normalized syntax nodes. Method signatures, adjacent declarations, and arbitrary statement fragments do not form candidates. Skipped files fail the gate with their path and reason.

Version `proscenium-deslop-roslyn-2` uses Roslyn syntax and UTF-8 source ranges. Structural hashes and MinHash signatures find candidates. Tree overlap can retain candidates with inserted code. Content comparison normalizes bound local and parameter names while preserving member names, operators, and literal values.

One shared syntax classifier distinguishes simple routing and data expressions from control flow and calculations. Distinct simple expressions are filtered; repeated identities and unclassified implementations remain findings. The classifier has no application-specific method, type, or API names. It does not prove that called code or user-defined conversions are equivalent.

Roslyn uses its default preprocessor symbols and honors source `#define` directives. Disabled branches are not analyzed. Malformed C# uses Roslyn recovery trees. Invalid UTF-8 fails the scan. Syntax nesting beyond 500 levels marks the file as skipped and fails the engineering gate.

The narrower candidate scope and local-name binding change counts, ranges, and report identities. The current detector does not promise identical output to the native tool. Native comparison tools, parser binaries, generated reports, and historical source snapshots are not included.

Build the detector:

```sh
dotnet build eng/code-quality/deslop/Deslop.csproj
```

Run the engineering checks:

```sh
eng/quality.sh
```

The CLI accepts a JSON array of paths relative to the scan root:

```sh
dotnet eng/code-quality/deslop/bin/Debug/net10.0/Deslop.dll \
  --root /path/to/source \
  --file-list /path/to/files.json \
  --output /path/to/report.json
```

Reports include discovered, analyzed, and skipped file counts. `--min-nodes` sets the CLI threshold. `--fail-over` sets a duplication percentage limit. The engineering gate always uses 30 nodes and rejects any reported duplication or skipped file.
