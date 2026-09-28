using System.Text.Json.Nodes;

namespace Proscenium.CodeQuality.Deslop;

// Port of report.rs, report_render.rs, report_metrics.rs, and report_weight.rs.
internal static class DeslopReport
{
    public static JsonObject Build(DeslopAnalysis analysis,
        IReadOnlyList<DeslopFile> files,
        string root,
        int minNodes,
        double? failOver)
    {
        var hidden = files.Select(file => DeslopVisibility.IsHidden(file, root)).ToArray();
        var lineIndices = files.Select(file => new DeslopLineIndex(file.Skipped ? [] : file.Source)).ToArray();
        var duplicatedLines = files.Select(_ => new HashSet<int>()).ToArray();
        var clusters = new List<JsonObject>();
        var hiddenCount = analysis.ClustersHidden;
        foreach (var cluster in analysis.Clusters)
        {
            if (cluster.Members.All(member => hidden[member.FileId]))
            {
                hiddenCount++;
                continue;
            }

            clusters.Add(ProjectCluster(cluster, files, hidden, lineIndices));
            foreach (var member in cluster.Members.Where(member => !hidden[member.FileId]))
            {
                var index = lineIndices[member.FileId];
                var end = Math.Min(member.End, index.SourceLength);
                var start = Math.Min(member.Start, end);
                var endLine = index.LineForOffset(Math.Max(Math.Max(end - 1, 0), start));
                for (var line = index.LineForOffset(start); line <= endLine; line++)
                {
                    duplicatedLines[member.FileId].Add(line);
                }
            }
        }

        clusters.Sort((left, right) => CompareClusters(left, right));
        for (var position = 0; position < clusters.Count; position++)
        {
            var rank = position + 1;
            clusters[position]["rank"] = rank;
            clusters[position]["rank_band"] = RankBand(rank, clusters.Count);
        }

        var metrics = BuildMetrics(files, lineIndices, duplicatedLines, clusters.Count, failOver);
        return new JsonObject
        {
            ["tool_version"] = "proscenium-deslop-roslyn-2",
            ["min_nodes"] = minNodes,
            ["files_discovered"] = files.Count,
            ["files_analysed"] = files.Count(file => !file.Skipped),
            ["files_skipped"] = files.Count(file => file.Skipped),
            ["skipped_files"] = new JsonArray(files.Where(file => file.Skipped).Select(file => (JsonNode)new JsonObject
            {
                ["path"] = file.Path,
                ["reason"] = "AST exceeds the 500-level depth limit.",
            }).ToArray()),
            ["clusters_hidden"] = hiddenCount,
            ["cache_stats"] = new JsonObject { ["hits"] = 0, ["misses"] = 0 },
            ["metrics"] = metrics,
            ["schema_doc"] = string.Empty,
            ["boilerplate_hints"] = new JsonArray(),
            ["embedding_provenance"] = null,
            ["clusters"] = new JsonArray(clusters.Cast<JsonNode>().ToArray()),
            ["literal_findings"] = new JsonArray(),
            ["literal_findings_total"] = 0,
            ["literal_findings_hidden"] = 0,
            ["literal_findings_capped"] = false,
            ["literal_max_findings"] = 0,
        };
    }

    private static JsonObject ProjectCluster(DeslopCluster cluster,
        IReadOnlyList<DeslopFile> files,
        bool[] hidden,
        DeslopLineIndex[] lines)
    {
        var canonical = cluster.Members.Min(member => member.NodeCount);
        var visibleCount = cluster.Members.Count(member => !hidden[member.FileId]);
        var occurrences = cluster.Members.Select(member => (JsonNode)new JsonObject
        {
            ["path"] = files[member.FileId].Path,
            ["start_byte"] = member.Start,
            ["end_byte"] = member.End,
            ["start_line"] = lines[member.FileId].LineForOffset(member.Start),
            ["end_line"] = lines[member.FileId].LineForOffset(Math.Max(member.End - 1, 0)),
            ["hidden"] = hidden[member.FileId],
        }).ToArray();
        return new JsonObject
        {
            ["id"] = cluster.Id,
            ["rank"] = 0,
            ["rank_band"] = string.Empty,
            ["mass"] = (ulong)canonical * (ulong)Math.Max(visibleCount - 1, 0),
            ["canonical_node_count"] = canonical,
            ["occurrences"] = new JsonArray(occurrences),
            ["occurrences_total"] = occurrences.Length,
            ["occurrence_count"] = visibleCount,
            ["occurrences_truncated"] = false,
        };
    }

    private static JsonObject BuildMetrics(IReadOnlyList<DeslopFile> files,
        DeslopLineIndex[] indices,
        HashSet<int>[] duplicatedLines,
        int clusterCount,
        double? failOver)
    {
        var fileRows = new List<JsonObject>();
        var folderSums = new Dictionary<string, (ulong Analyzed, ulong Duplicated)>(StringComparer.Ordinal);
        ulong analyzed = 0;
        ulong duplicated = 0;
        for (var index = 0; index < files.Count; index++)
        {
            if (files[index].Skipped)
            {
                continue;
            }

            var loc = indices[index].PhysicalLines;
            var dup = (ulong)duplicatedLines[index].Count;
            analyzed += loc;
            duplicated += dup;
            var path = files[index].Path;
            fileRows.Add(MetricRow(path, loc, dup));
            var segments = path.Split('/');
            for (var length = 1; length < segments.Length; length++)
            {
                var prefix = string.Join('/', segments.Take(length));
                var prior = folderSums.GetValueOrDefault(prefix);
                folderSums[prefix] = (prior.Analyzed + loc, prior.Duplicated + dup);
            }
        }

        fileRows.Sort(CompareMetrics);
        var folders = folderSums.Where(pair => pair.Value.Duplicated > 0)
            .Select(pair => MetricRow(pair.Key, pair.Value.Analyzed, pair.Value.Duplicated)).ToList();
        folders.Sort(CompareMetrics);
        var percent = Percent(duplicated, analyzed);
        return new JsonObject
        {
            ["analysed_loc"] = analyzed,
            ["duplicated_loc"] = duplicated,
            ["duplication_percent"] = percent,
            ["clusters_total"] = clusterCount,
            ["duplicated_files"] = duplicatedLines.Count(lines => lines.Count != 0),
            ["threshold"] = new JsonObject
            {
                ["percent"] = failOver ?? 0,
                ["breached"] = failOver.HasValue && percent > failOver.Value,
                ["source"] = failOver.HasValue ? "cli" : "none",
            },
            ["per_file"] = new JsonArray(fileRows.Cast<JsonNode>().ToArray()),
            ["folders"] = new JsonArray(folders.Cast<JsonNode>().ToArray()),
        };
    }

    private static JsonObject MetricRow(string path, ulong analyzed, ulong duplicated) => new()
    {
        ["path"] = path,
        ["analysed_loc"] = analyzed,
        ["duplicated_loc"] = duplicated,
        ["duplication_percent"] = Percent(duplicated, analyzed),
    };

    private static double Percent(ulong numerator, ulong denominator) => denominator == 0
        ? 0
        : Math.Clamp((double)Math.Min(numerator, uint.MaxValue) / Math.Min(denominator, uint.MaxValue) * 100.0, 0, 100);

    private static int CompareClusters(JsonObject left, JsonObject right)
    {
        var byMass = right["mass"]!.GetValue<ulong>().CompareTo(left["mass"]!.GetValue<ulong>());
        return byMass != 0 ? byMass : StringComparer.Ordinal.Compare(
            left["id"]!.GetValue<string>(), right["id"]!.GetValue<string>());
    }

    private static int CompareMetrics(JsonObject left, JsonObject right)
    {
        var byPercent = right["duplication_percent"]!.GetValue<double>()
            .CompareTo(left["duplication_percent"]!.GetValue<double>());
        return byPercent != 0 ? byPercent : DeslopPathComparer.Instance.Compare(
            left["path"]!.GetValue<string>(), right["path"]!.GetValue<string>());
    }

    private static string RankBand(int rank, int total) => rank <= (total + 99) / 100 ? "worst"
        : rank <= (total + 9) / 10 ? "top10"
        : rank <= (total + 1) / 2 ? "mid" : "faint";
}
