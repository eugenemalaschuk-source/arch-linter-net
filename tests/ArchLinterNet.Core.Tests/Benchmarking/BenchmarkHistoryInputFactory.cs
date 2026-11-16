using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Tests.Benchmarking;

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
        using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
        List<BenchmarkHistoryWorkloadInput> workloads = [];
        foreach (JsonElement family in catalog.RootElement.GetProperty("history_workloads").EnumerateArray())
        foreach (JsonElement workload in family.GetProperty("workloads").EnumerateArray())
            workloads.Add(Create(
                family.GetProperty("owner_issue").GetString()!,
                family.GetProperty("family").GetString()!,
                family.GetProperty("dimensions").EnumerateArray().Select(item => item.GetString()!).ToArray(),
                workload));

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
            dimensions.Add(dimension, workload.GetProperty(dimension).GetInt32());

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
            _ => throw new InvalidOperationException($"Unknown History benchmark family '{family}'.")
        };
        var inputPayload = new
        {
            schema = "benchmark-history-input/v1",
            generator_version = "3",
            owner_issue = ownerIssue,
            family,
            workload_id = id,
            dimensions,
            before_text = beforeText,
            after_text = afterText,
            records
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
        SortedDictionary<string, int> dimensions,
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
            int line = (int)((long)(edit + 1) * lineCount / (editCount + 1));
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
            ["unchanged_lines"] = lineCount - editCount
        });
    }

    private static BenchmarkHistoryExpectedOutput CreateTaskOccurrences(
        string family,
        SortedDictionary<string, int> dimensions,
        List<BenchmarkHistoryInputRecord> records)
    {
        int taskCount = dimensions["task_count"];
        int commitCount = dimensions["commit_count"];
        int occurrenceCount = dimensions["occurrence_count"];
        bool overlapping = family.EndsWith("overlapping", StringComparison.Ordinal);
        for (int commit = 0; commit < commitCount; commit++)
            records.Add(new BenchmarkHistoryInputRecord("commit", commit, null, null));

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

        long candidatePairs = overlapping ? (long)taskCount * (taskCount - 1) / 2 : 0;
        return new BenchmarkHistoryExpectedOutput(new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["candidate_task_pairs"] = candidatePairs,
            ["commit_count"] = commitCount,
            ["occurrence_count"] = occurrenceCount,
            ["task_count"] = taskCount
        });
    }

    private static BenchmarkHistoryExpectedOutput CreateCoChangeGraph(
        string family,
        SortedDictionary<string, int> dimensions,
        List<BenchmarkHistoryInputRecord> records)
    {
        int vertexCount = dimensions["vertex_count"];
        int edgeCount = dimensions["edge_count"];
        int componentCount = dimensions["component_count"];
        for (int vertex = 0; vertex < vertexCount; vertex++)
            records.Add(new BenchmarkHistoryInputRecord("vertex", vertex, null, $"src/file-{vertex:000000}.cs"));

        if (family == "co_change_dense_component")
            for (int first = 0; first < vertexCount; first++)
            for (int second = first + 1; second < vertexCount; second++)
                records.Add(new BenchmarkHistoryInputRecord("co_change_edge", first, second, null));
        else
            for (int edge = 0; edge < edgeCount; edge++)
                records.Add(new BenchmarkHistoryInputRecord("co_change_edge", edge * 2, edge * 2 + 1, null));

        return new BenchmarkHistoryExpectedOutput(new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["component_count"] = componentCount,
            ["edge_count"] = edgeCount,
            ["vertex_count"] = vertexCount
        });
    }

    private static BenchmarkHistoryExpectedOutput CreateAncestryGraph(
        string family,
        SortedDictionary<string, int> dimensions,
        List<BenchmarkHistoryInputRecord> records)
    {
        int commitCount = dimensions["reachable_commit_count"];
        int queryCount = dimensions["query_endpoint_count"];
        int mergeCount = dimensions.TryGetValue("merge_count", out int merges) ? merges : 0;
        int renameEventCount = dimensions.TryGetValue("rename_event_count", out int renameEvents) ? renameEvents : 0;
        int mainlineCommitCount = commitCount - mergeCount;
        int positiveQueryCount = (queryCount + 1) / 2;
        int availableQueryAncestors = mergeCount > 0 ? mergeCount : mainlineCommitCount;
        if (mainlineCommitCount < 3 || queryCount < 2 || positiveQueryCount > availableQueryAncestors ||
            renameEventCount > mainlineCommitCount - 1)
            throw new InvalidOperationException(
                "Ancestry workload dimensions cannot produce the declared graph and query shapes.");

        HashSet<int> mergeOrdinals = [];
        for (int merge = 0; merge < mergeCount; merge++)
        {
            int mainlineOrdinal = 2 + (int)((long)(merge + 1) * (mainlineCommitCount - 3) / (mergeCount + 1));
            if (!mergeOrdinals.Add(mainlineOrdinal))
                throw new InvalidOperationException("Merged-DAG scale points must have distinct merge positions.");
        }

        List<int> mainlineCommits = new(mainlineCommitCount);
        List<int> sideBranchCommits = new(mergeCount);
        int nextCommitId = 0;
        for (int mainlineOrdinal = 0; mainlineOrdinal < mainlineCommitCount; mainlineOrdinal++)
        {
            int? sideBranchCommit = null;
            if (mergeOrdinals.Contains(mainlineOrdinal))
            {
                int branchCommit = nextCommitId++;
                sideBranchCommit = branchCommit;
                sideBranchCommits.Add(branchCommit);
                records.Add(new BenchmarkHistoryInputRecord("commit", branchCommit, null, null));
                records.Add(new BenchmarkHistoryInputRecord(
                    "parent_edge", branchCommit, mainlineCommits[mainlineOrdinal - 2], "branch"));
            }

            int mainlineCommit = nextCommitId++;
            mainlineCommits.Add(mainlineCommit);
            records.Add(new BenchmarkHistoryInputRecord("commit", mainlineCommit, null, null));
            if (mainlineOrdinal > 0)
                records.Add(new BenchmarkHistoryInputRecord(
                    "parent_edge", mainlineCommit, mainlineCommits[mainlineOrdinal - 1], null));

            if (sideBranchCommit.HasValue)
                records.Add(new BenchmarkHistoryInputRecord(
                    "parent_edge", mainlineCommit, sideBranchCommit.Value, "merge"));
        }

        if (family == "ancestry_query_long_rename_chain")
            for (int rename = 0; rename < renameEventCount; rename++)
                records.Add(new BenchmarkHistoryInputRecord(
                    "rename_event", mainlineCommits[rename + 1], null, null)
                {
                    SourcePath = $"src/history/path-{rename:000000}.cs",
                    TargetPath = $"src/history/path-{rename + 1:000000}.cs"
                });

        for (int query = 0; query < queryCount; query++)
        {
            bool expectedAncestry = query % 2 == 0;
            int ancestor = expectedAncestry
                ? mergeCount > 0 ? sideBranchCommits[query / 2] : mainlineCommits[query / 2]
                : mainlineCommits[^1];
            int descendant = expectedAncestry
                ? mainlineCommits[^1]
                : mainlineCommits[query / 2 % (mainlineCommitCount - 1)];
            records.Add(new BenchmarkHistoryInputRecord("ancestry_query", ancestor, descendant, null)
            {
                ExpectedAncestry = expectedAncestry
            });
        }

        AncestryGraphSummary summary = SummarizeAncestryGraph(records);
        return new BenchmarkHistoryExpectedOutput(new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["merge_count"] = records.Count(record => record is { Kind: "parent_edge", Value: "merge" }),
            ["negative_ancestry_query_count"] = summary.NegativeQueryCount,
            ["parent_edge_count"] = records.Count(record => record.Kind == "parent_edge"),
            ["positive_ancestry_query_count"] = summary.PositiveQueryCount,
            ["query_endpoint_count"] = records.Count(record => record.Kind == "ancestry_query"),
            ["reachable_commit_count"] = records.Count(record => record.Kind == "commit"),
            ["rename_event_count"] = records.Count(record => record.Kind == "rename_event"),
            ["strict_ancestor_closure_entry_count"] = summary.StrictAncestorClosureEntryCount,
            ["strict_ancestor_query_count"] = summary.PositiveQueryCount
        });
    }

    private static AncestryGraphSummary SummarizeAncestryGraph(IReadOnlyList<BenchmarkHistoryInputRecord> records)
    {
        var parents = records
            .Where(record => record.Kind == "commit")
            .ToDictionary(record => record.First, _ => new List<int>());
        foreach (BenchmarkHistoryInputRecord edge in records.Where(record => record.Kind == "parent_edge"))
            parents[edge.First].Add(edge.Second!.Value);

        int commitCount = parents.Count;
        int wordCount = checked((commitCount + 63) / 64);
        ulong[][] ancestorBitsByCommit = new ulong[commitCount][];
        long strictAncestorClosureEntryCount = 0;
        for (int commit = 0; commit < commitCount; commit++)
        {
            ulong[] ancestorBits = new ulong[wordCount];
            foreach (int parent in parents[commit])
            {
                if (parent >= commit)
                    throw new InvalidOperationException(
                        "Ancestry parent edges must point backward through commit order.");

                ulong[] parentAncestors = ancestorBitsByCommit[parent];
                for (int word = 0; word < wordCount; word++) ancestorBits[word] |= parentAncestors[word];

                ancestorBits[parent / 64] |= 1UL << (parent % 64);
            }

            ancestorBitsByCommit[commit] = ancestorBits;
            foreach (ulong word in ancestorBits) strictAncestorClosureEntryCount += BitOperations.PopCount(word);
        }

        int positiveQueryCount = 0;
        int negativeQueryCount = 0;
        foreach (BenchmarkHistoryInputRecord query in records.Where(record => record.Kind == "ancestry_query"))
        {
            if (!query.ExpectedAncestry.HasValue)
                throw new InvalidOperationException("Every ancestry query must record its expected answer.");

            bool actualAncestry = query.First != query.Second!.Value
                                  && (ancestorBitsByCommit[query.Second.Value][query.First / 64] &
                                      (1UL << (query.First % 64))) != 0;
            if (actualAncestry != query.ExpectedAncestry.Value)
                throw new InvalidOperationException("A generated ancestry query does not match the parent graph.");

            if (actualAncestry)
                positiveQueryCount++;
            else
                negativeQueryCount++;
        }

        return new AncestryGraphSummary(
            strictAncestorClosureEntryCount,
            positiveQueryCount,
            negativeQueryCount);
    }

    private sealed record AncestryGraphSummary(
        long StrictAncestorClosureEntryCount,
        int PositiveQueryCount,
        int NegativeQueryCount);
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

internal sealed record BenchmarkHistoryInputRecord(string Kind, int First, int? Second, string? Value)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ExpectedAncestry { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePath { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetPath { get; init; }
}

internal sealed record BenchmarkHistoryExpectedOutput(IReadOnlyDictionary<string, long> Values);
