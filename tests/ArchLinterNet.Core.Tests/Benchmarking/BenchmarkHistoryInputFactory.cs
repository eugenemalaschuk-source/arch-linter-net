using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

/// <summary>Deterministic, algorithm-neutral inputs shared by the owning History benchmark tasks.</summary>
internal static class BenchmarkHistoryInputFactory
{
    public static IReadOnlyList<BenchmarkHistoryWorkloadInput> ReadCatalog(string repositoryRoot)
    {
        string catalogPath = Path.Combine(
            repositoryRoot,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "workload-catalog.json");
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
        List<BenchmarkHistoryWorkloadInput> workloads = [];
        foreach (JsonElement family in catalog.RootElement.GetProperty("history_workloads").EnumerateArray())
        {
            foreach (JsonElement workload in family.GetProperty("workloads").EnumerateArray())
            {
                workloads.Add(Create(
                    family.GetProperty("owner_issue").GetString()!,
                    family.GetProperty("family").GetString()!,
                    family.GetProperty("dimensions").EnumerateArray().Select(item => item.GetString()!).ToArray(),
                    workload));
            }
        }

        return workloads;
    }

    private static BenchmarkHistoryWorkloadInput Create(
        string ownerIssue,
        string family,
        IReadOnlyList<string> dimensionNames,
        JsonElement workload)
    {
        string id = workload.GetProperty("id").GetString()!;
        SortedDictionary<string, int> dimensions = new(StringComparer.Ordinal);
        foreach (string dimension in dimensionNames)
        {
            dimensions.Add(dimension, workload.GetProperty(dimension).GetInt32());
        }

        List<BenchmarkHistoryInputRecord> records = [];
        string? beforeText = null;
        string? afterText = null;
        BenchmarkHistoryExpectedOutput output = family switch
        {
            "line_churn_sparse_edits" or "line_churn_dense_edits" =>
                CreateLineChurn(dimensions, records, out beforeText, out afterText),
            "task_commit_occurrences_sparse" or "task_commit_occurrences_overlapping" =>
                CreateTaskOccurrences(family, dimensions, records),
            "co_change_disconnected_components" or "co_change_dense_component" =>
                CreateCoChangeGraph(family, dimensions, records),
            "ancestry_query_long_rename_chain" or "ancestry_query_merged_dag" =>
                CreateAncestryGraph(family, dimensions, records),
            _ => throw new InvalidOperationException($"Unknown History benchmark family '{family}'."),
        };
        var inputPayload = new
        {
            schema = "benchmark-history-input/v1",
            generator_version = "1",
            owner_issue = ownerIssue,
            family,
            workload_id = id,
            dimensions,
            before_text = beforeText,
            after_text = afterText,
            records,
        };
        string canonicalInput = BenchmarkJson.Serialize(inputPayload);
        string canonicalOutput = BenchmarkJson.Serialize(output.Values);
        return new BenchmarkHistoryWorkloadInput(
            ownerIssue,
            family,
            id,
            dimensions,
            beforeText,
            afterText,
            records,
            canonicalInput,
            BenchmarkIdentity.Sha256(canonicalInput),
            canonicalOutput,
            BenchmarkIdentity.Sha256(canonicalOutput));
    }

    private static BenchmarkHistoryExpectedOutput CreateLineChurn(
        IReadOnlyDictionary<string, int> dimensions,
        List<BenchmarkHistoryInputRecord> records,
        out string beforeText,
        out string afterText)
    {
        int lineCount = dimensions["line_count"];
        int editCount = dimensions["edit_count"];
        string[] beforeLines = Enumerable.Range(0, lineCount).Select(index => $"line-{index:000000}").ToArray();
        string[] afterLines = [.. beforeLines];
        for (int edit = 0; edit < editCount; edit++)
        {
            int line = (int)(((long)(edit + 1) * lineCount) / (editCount + 1));
            string replacement = $"edited-{edit:000000}";
            records.Add(new BenchmarkHistoryInputRecord("line_edit", line, null, replacement));
            afterLines[line] = replacement;
        }

        beforeText = string.Join('\n', beforeLines) + "\n";
        afterText = string.Join('\n', afterLines) + "\n";
        return new BenchmarkHistoryExpectedOutput(new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["additions"] = editCount,
            ["deletions"] = editCount,
            ["unchanged_lines"] = lineCount - editCount,
        });
    }

    private static BenchmarkHistoryExpectedOutput CreateTaskOccurrences(
        string family,
        IReadOnlyDictionary<string, int> dimensions,
        List<BenchmarkHistoryInputRecord> records)
    {
        int taskCount = dimensions["task_count"];
        int commitCount = dimensions["commit_count"];
        int occurrenceCount = dimensions["occurrence_count"];
        bool overlapping = family.EndsWith("overlapping", StringComparison.Ordinal);
        for (int commit = 0; commit < commitCount; commit++)
        {
            records.Add(new BenchmarkHistoryInputRecord("commit", commit, null, null));
        }

        for (int occurrence = 0; occurrence < occurrenceCount; occurrence++)
        {
            int task = occurrence % taskCount;
            int repetition = occurrence / taskCount;
            int commit = (task * 17 + repetition) % commitCount;
            string path = overlapping ? "src/shared.cs" : $"src/file-{task:000000}.cs";
            records.Add(new BenchmarkHistoryInputRecord(
                "task_commit_occurrence",
                commit,
                task,
                $"issue#{task + 1:000000}|{path}"));
        }

        long candidatePairs = overlapping ? ((long)taskCount * (taskCount - 1)) / 2 : 0;
        return new BenchmarkHistoryExpectedOutput(new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["candidate_task_pairs"] = candidatePairs,
            ["commit_count"] = commitCount,
            ["occurrence_count"] = occurrenceCount,
            ["task_count"] = taskCount,
        });
    }

    private static BenchmarkHistoryExpectedOutput CreateCoChangeGraph(
        string family,
        IReadOnlyDictionary<string, int> dimensions,
        List<BenchmarkHistoryInputRecord> records)
    {
        int vertexCount = dimensions["vertex_count"];
        int edgeCount = dimensions["edge_count"];
        int componentCount = dimensions["component_count"];
        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            records.Add(new BenchmarkHistoryInputRecord("vertex", vertex, null, $"src/file-{vertex:000000}.cs"));
        }

        if (family == "co_change_dense_component")
        {
            for (int first = 0; first < vertexCount; first++)
            {
                for (int second = first + 1; second < vertexCount; second++)
                {
                    records.Add(new BenchmarkHistoryInputRecord("co_change_edge", first, second, null));
                }
            }
        }
        else
        {
            for (int edge = 0; edge < edgeCount; edge++)
            {
                records.Add(new BenchmarkHistoryInputRecord("co_change_edge", edge * 2, edge * 2 + 1, null));
            }
        }

        return new BenchmarkHistoryExpectedOutput(new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["component_count"] = componentCount,
            ["edge_count"] = edgeCount,
            ["vertex_count"] = vertexCount,
        });
    }

    private static BenchmarkHistoryExpectedOutput CreateAncestryGraph(
        string family,
        IReadOnlyDictionary<string, int> dimensions,
        List<BenchmarkHistoryInputRecord> records)
    {
        int commitCount = dimensions["reachable_commit_count"];
        int queryCount = dimensions["query_endpoint_count"];
        int mergeCount = dimensions.TryGetValue("merge_count", out int merges) ? merges : 0;
        long retainedEntries = dimensions["retained_ancestor_entry_count"];
        for (int commit = 0; commit < commitCount; commit++)
        {
            records.Add(new BenchmarkHistoryInputRecord("commit", commit, null, null));
            if (commit > 0)
            {
                records.Add(new BenchmarkHistoryInputRecord("parent_edge", commit, commit - 1, null));
            }
        }

        for (int merge = 0; merge < mergeCount; merge++)
        {
            int commit = 2 + (int)(((long)(merge + 1) * (commitCount - 3)) / (mergeCount + 1));
            records.Add(new BenchmarkHistoryInputRecord("parent_edge", commit, commit - 2, "merge"));
        }

        for (int query = 0; query < queryCount; query++)
        {
            records.Add(new BenchmarkHistoryInputRecord("ancestry_query", query, commitCount - 1, null));
        }

        return new BenchmarkHistoryExpectedOutput(new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["merge_count"] = mergeCount,
            ["query_endpoint_count"] = queryCount,
            ["reachable_commit_count"] = commitCount,
            ["retained_ancestor_entry_count"] = retainedEntries,
            ["strict_ancestor_query_count"] = queryCount,
        });
    }
}

internal sealed record BenchmarkHistoryWorkloadInput(
    string OwnerIssue,
    string Family,
    string WorkloadId,
    IReadOnlyDictionary<string, int> Dimensions,
    string? BeforeText,
    string? AfterText,
    IReadOnlyList<BenchmarkHistoryInputRecord> Records,
    string CanonicalInputJson,
    string InputSha256,
    string ExpectedOutputJson,
    string ExpectedOutputSha256);

internal sealed record BenchmarkHistoryInputRecord(string Kind, int First, int? Second, string? Value);

internal sealed record BenchmarkHistoryExpectedOutput(IReadOnlyDictionary<string, long> Values);
