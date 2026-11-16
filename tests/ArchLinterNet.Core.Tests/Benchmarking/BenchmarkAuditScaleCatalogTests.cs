using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.Benchmarking;

[TestFixture]
public sealed class BenchmarkAuditScaleCatalogTests
{
    [Test]
    public void IndependentAuditDimensionsAndExecutableGraphShapes_HaveThreeScalePoints()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        string path = Path.Combine(
            root,
            "docs",
            "internal",
            "large-solution-benchmarking",
            "workload-catalog.json");
        using var catalog = JsonDocument.Parse(File.ReadAllText(path));
        HashSet<string> workloadIds = new(StringComparer.Ordinal);

        JsonElement auditFamilies = catalog.RootElement.GetProperty("audit_scale_families");
        Assert.That(auditFamilies.GetArrayLength(), Is.EqualTo(8));
        foreach (JsonElement family in auditFamilies.EnumerateArray())
        {
            string dimension = family.GetProperty("dimension").GetString()!;
            JsonElement fixedDimensions = family.GetProperty("fixed_dimensions");
            Assert.That(fixedDimensions.TryGetProperty(dimension, out _), Is.False,
                $"{dimension} must remain an independently varied dimension.");
            AssertThreeNamedPoints(family, workloadIds, "value");
        }

        JsonElement graphFamilies = catalog.RootElement.GetProperty("contract_layer_graph_scale_families");
        Assert.That(graphFamilies.GetArrayLength(), Is.EqualTo(3));
        foreach (JsonElement family in graphFamilies.EnumerateArray())
            AssertThreeNamedPoints(family, workloadIds, "value");
    }

    private static void AssertThreeNamedPoints(JsonElement family, HashSet<string> workloadIds, string valueProperty)
    {
        JsonElement workloads = family.GetProperty("workloads");
        Assert.That(workloads.GetArrayLength(), Is.EqualTo(3), family.GetProperty("dimension").GetString());
        foreach (JsonElement workload in workloads.EnumerateArray())
        {
            Assert.That(workloadIds.Add(workload.GetProperty("id").GetString()!), Is.True);
            Assert.That(workload.GetProperty(valueProperty).GetInt32(), Is.GreaterThan(0));
        }
    }
}
