using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.History.Evidence;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class BenchmarkHistoryWorkloadCatalogTests
{
    [Test]
    public void SharedHistoryFamilies_HaveThreeNamedAndIndependentScalePoints()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        string path = Path.Combine(
            root,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "workload-catalog.json");
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement families = catalog.RootElement.GetProperty("history_workloads");
        HashSet<string> workloadIds = new(StringComparer.Ordinal);

        foreach (JsonElement family in families.EnumerateArray())
        {
            JsonElement dimensions = family.GetProperty("dimensions");
            JsonElement workloads = family.GetProperty("workloads");
            Assert.That(dimensions.GetArrayLength(), Is.GreaterThan(0), family.GetProperty("family").GetString());
            Assert.That(workloads.GetArrayLength(), Is.EqualTo(3), family.GetProperty("family").GetString());

            foreach (JsonElement workload in workloads.EnumerateArray())
            {
                Assert.That(workloadIds.Add(workload.GetProperty("id").GetString()!), Is.True);
                foreach (JsonElement dimension in dimensions.EnumerateArray())
                {
                    Assert.That(workload.TryGetProperty(dimension.GetString()!, out JsonElement value), Is.True,
                        $"{workload.GetProperty("id").GetString()} is missing dimension {dimension.GetString()}.");
                    Assert.That(value.GetInt32(), Is.GreaterThan(0));
                }
            }
        }

        Assert.That(families.GetArrayLength(), Is.GreaterThanOrEqualTo(7));
    }

    [Test]
    public void SharedHistoryInputs_AreDeterministicAndHaveVerifiedOutputChecksums()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        IReadOnlyList<BenchmarkHistoryWorkloadInput> workloads = BenchmarkHistoryInputFactory.ReadCatalog(root);
        Dictionary<string, BenchmarkHistoryWorkloadInput> replayById = BenchmarkHistoryInputFactory.ReadCatalog(root)
            .ToDictionary(workload => workload.WorkloadId, StringComparer.Ordinal);
        Assert.That(workloads, Has.Count.EqualTo(24));
        HashSet<string> ids = new(StringComparer.Ordinal);

        foreach (BenchmarkHistoryWorkloadInput input in workloads)
        {
            BenchmarkHistoryWorkloadInput replay = replayById[input.WorkloadId];
            Assert.Multiple(() =>
            {
                Assert.That(ids.Add(input.WorkloadId), Is.True);
                Assert.That(replay.CanonicalInputJson, Is.EqualTo(input.CanonicalInputJson));
                Assert.That(replay.InputSha256, Is.EqualTo(input.InputSha256));
                Assert.That(replay.ExpectedOutputJson, Is.EqualTo(input.ExpectedOutputJson));
                Assert.That(replay.ExpectedOutputSha256, Is.EqualTo(input.ExpectedOutputSha256));
            });

            SortedDictionary<string, long> expected = RecomputeExpectedOutput(input);
            string outputJson = BenchmarkJson.Serialize(expected);
            Assert.That(outputJson, Is.EqualTo(input.ExpectedOutputJson), input.WorkloadId);
            Assert.That(BenchmarkIdentity.Sha256(outputJson), Is.EqualTo(input.ExpectedOutputSha256), input.WorkloadId);
        }
    }

    private static SortedDictionary<string, long> RecomputeExpectedOutput(BenchmarkHistoryWorkloadInput input)
    {
        IEnumerable<BenchmarkHistoryInputRecord> records = input.Records;
        return input.Family switch
        {
            "line_churn_sparse_edits" or "line_churn_dense_edits" => RecomputeLineOutput(input, records),
            "task_commit_occurrences_sparse" or "task_commit_occurrences_overlapping" => RecomputeTaskOutput(input, records),
            "co_change_disconnected_components" or "co_change_dense_component" => RecomputeCoChangeOutput(input, records),
            "ancestry_query_long_rename_chain" or "ancestry_query_merged_dag" => RecomputeAncestryOutput(input, records),
            _ => throw new AssertionException($"Unknown History workload family '{input.Family}'."),
        };
    }

    private static SortedDictionary<string, long> RecomputeLineOutput(
        BenchmarkHistoryWorkloadInput input,
        IEnumerable<BenchmarkHistoryInputRecord> records)
    {
        int editCount = records.Count(record => record.Kind == "line_edit");
        int lineCount = input.Dimensions["line_count"];
        if (input.WorkloadId.EndsWith("small", StringComparison.Ordinal))
        {
            (long additions, long deletions) = LineChurnCalculator.Compute(
                Encoding.UTF8.GetBytes(input.BeforeText!),
                Encoding.UTF8.GetBytes(input.AfterText!));
            Assert.That((additions, deletions), Is.EqualTo(((long)editCount, (long)editCount)), input.WorkloadId);
        }

        return new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["additions"] = editCount,
            ["deletions"] = editCount,
            ["unchanged_lines"] = lineCount - editCount,
        };
    }

    private static SortedDictionary<string, long> RecomputeTaskOutput(
        BenchmarkHistoryWorkloadInput input,
        IEnumerable<BenchmarkHistoryInputRecord> records)
    {
        BenchmarkHistoryInputRecord[] occurrences = records.Where(record => record.Kind == "task_commit_occurrence").ToArray();
        int commitCount = records.Count(record => record.Kind == "commit");
        int taskCount = input.Dimensions["task_count"];
        Assert.That(occurrences.Select(record => record.Second).Distinct().Count(), Is.EqualTo(taskCount), input.WorkloadId);
        bool overlapping = input.Family.EndsWith("overlapping", StringComparison.Ordinal);
        return new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["candidate_task_pairs"] = overlapping ? (long)taskCount * (taskCount - 1) / 2 : 0,
            ["commit_count"] = commitCount,
            ["occurrence_count"] = occurrences.Length,
            ["task_count"] = taskCount,
        };
    }

    private static SortedDictionary<string, long> RecomputeCoChangeOutput(
        BenchmarkHistoryWorkloadInput input,
        IEnumerable<BenchmarkHistoryInputRecord> records)
    {
        int vertexCount = records.Count(record => record.Kind == "vertex");
        BenchmarkHistoryInputRecord[] edges = records.Where(record => record.Kind == "co_change_edge").ToArray();
        Dictionary<int, List<int>> adjacency = Enumerable.Range(0, vertexCount)
            .ToDictionary(vertex => vertex, _ => new List<int>());
        foreach (BenchmarkHistoryInputRecord edge in edges)
        {
            adjacency[edge.First].Add(edge.Second!.Value);
            adjacency[edge.Second.Value].Add(edge.First);
        }

        HashSet<int> visited = [];
        int componentCount = 0;
        foreach (int vertex in adjacency.Keys)
        {
            if (!visited.Add(vertex))
            {
                continue;
            }

            componentCount++;
            Stack<int> pending = new();
            pending.Push(vertex);
            while (pending.TryPop(out int current))
            {
                foreach (int neighbor in adjacency[current])
                {
                    if (visited.Add(neighbor))
                    {
                        pending.Push(neighbor);
                    }
                }
            }
        }

        return new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["component_count"] = componentCount,
            ["edge_count"] = edges.Length,
            ["vertex_count"] = vertexCount,
        };
    }

    private static SortedDictionary<string, long> RecomputeAncestryOutput(
        BenchmarkHistoryWorkloadInput input,
        IEnumerable<BenchmarkHistoryInputRecord> records)
    {
        BenchmarkHistoryInputRecord[] edges = records.Where(record => record.Kind == "parent_edge").ToArray();
        BenchmarkHistoryInputRecord[] queries = records.Where(record => record.Kind == "ancestry_query").ToArray();
        Dictionary<int, List<int>> parents = Enumerable.Range(0, input.Dimensions["reachable_commit_count"])
            .ToDictionary(commit => commit, _ => new List<int>());
        foreach (BenchmarkHistoryInputRecord edge in edges)
        {
            parents[edge.First].Add(edge.Second!.Value);
        }

        int satisfied = 0;
        foreach (BenchmarkHistoryInputRecord query in queries)
        {
            HashSet<int> visited = [];
            Stack<int> pending = new();
            pending.Push(query.Second!.Value);
            while (pending.TryPop(out int current))
            {
                if (!visited.Add(current))
                {
                    continue;
                }

                foreach (int parent in parents[current])
                {
                    pending.Push(parent);
                }
            }

            if (visited.Contains(query.First) && query.First != query.Second.Value)
            {
                satisfied++;
            }
        }

        return new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["merge_count"] = input.Dimensions.TryGetValue("merge_count", out int mergeCount) ? mergeCount : 0,
            ["query_endpoint_count"] = queries.Length,
            ["reachable_commit_count"] = input.Dimensions["reachable_commit_count"],
            ["retained_ancestor_entry_count"] = input.Dimensions["retained_ancestor_entry_count"],
            ["strict_ancestor_query_count"] = satisfied,
        };
    }
}
