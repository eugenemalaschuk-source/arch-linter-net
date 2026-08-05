using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Schema;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// A real AnalysisCacheStore-serialized AnalysisCacheEntryV1 validates against the exact
// release-matched resource returned by PackagedSchemaRegistry, including a violation carrying a
// real closed-set Payload so the schema's "$kind"/"value" envelope is exercised.
[TestFixture]
public sealed class AnalysisCacheSchemaValidationTests
{
    private static JsonSchema LoadSchema()
    {
        PackagedSchemaRegistry registry = new();
        Assert.That(registry.TryRead("analysis-cache", out string schemaText), Is.True);
        return JsonSchema.FromText(schemaText);
    }

    private static AnalysisCacheEntryV1 BuildSampleEntry(string cacheRootPath)
    {
        ArchitectureViolation violationWithPayload = new(
            "no_infra_from_domain", "R001", "MyApp.Domain.Order", "MyApp.Infrastructure",
            new[] { "MyApp.Infrastructure.Db" })
        {
            Payload = new DependencyPayload("Domain", "Infrastructure", new[] { "MyApp.Application" }),
        };

        AnalysisCacheOutcomeV1 outcome = new(
            Passed: false,
            Violations: new[] { violationWithPayload },
            Cycles: Array.Empty<string>(),
            CoverageFindings: Array.Empty<ArchitectureViolation>(),
            CoverageConfig: "off",
            UnmatchedIgnoredViolations: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            UnmatchedIgnoredViolationsConfig: "off",
            PolicyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
            PolicyConsistencyConfig: "off",
            ClassificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
            ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>());

        AnalysisCacheEntryV1 withoutDigest = new()
        {
            FormatVersion = AnalysisCacheEnvelope.FormatVersion,
            KeyDigest = new string('a', 64),
            Mode = "strict",
            ToolVersion = AnalysisCacheEnvelope.ToolVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CompletionStatus = AnalysisCacheEntryCompletionStatus.Success,
            ProjectManifests = new[]
            {
                new AnalysisCacheProjectManifest("src/A/A.csproj", "digest-a", CacheEligibility.VerifiedCacheEligible),
            },
            ArtifactManifests = Array.Empty<AnalysisCacheArtifactManifest>(),
            Outcome = outcome,
            WorkProvenance = new AnalysisCacheWorkProvenanceV1(
                AssemblyLoads: 10,
                FactIndexMaterializations: 1,
                SourceScanPasses: 1,
                ContractExecutions: 2,
                ArtifactBytesLoaded: 155392),
            ContentDigest = string.Empty,
        };

        return withoutDigest with { ContentDigest = AnalysisCacheContentDigest.Compute(withoutDigest, cacheRootPath) };
    }

    [Test]
    public void RealCacheEntry_ValidatesAgainstSchema()
    {
        string cacheRoot = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-schema-key-tests", Guid.NewGuid().ToString("N"));
        try
        {
            AnalysisCacheEntryV1 entry = BuildSampleEntry(cacheRoot);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(entry, AnalysisCacheJson.Options);
            string json = System.Text.Encoding.UTF8.GetString(bytes);

            EvaluationResults evaluation = LoadSchema().Evaluate(
                JsonNode.Parse(json), new EvaluationOptions { OutputFormat = OutputFormat.List });

            Assert.That(evaluation.IsValid, Is.True,
                string.Join(Environment.NewLine, evaluation.Details.Where(d => !d.IsValid)
                    .Select(d => d.EvaluationPath + ": " + string.Join(",", d.Errors?.Values ?? []))));
        }
        finally
        {
            if (Directory.Exists(cacheRoot))
            {
                Directory.Delete(cacheRoot, recursive: true);
            }
        }
    }

    [Test]
    public void RealCacheEntry_RoundTripsThroughStore_AndValidatesAgainstSchema()
    {
        string root = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-schema-tests", Guid.NewGuid().ToString("N"));
        AnalysisCacheLocation location = new(root, AnalysisCacheMode.ExplicitPath);
        try
        {
            AnalysisCacheEntryV1 entry = BuildSampleEntry(root);
            AnalysisCacheKey key = new("policy", entry.Mode, null, "contracts", "workspace", null, null, null, null);
            AnalysisCacheStore.PutResult putResult = AnalysisCacheStore.Put(
                location, key, entry.ProjectManifests, entry.Outcome);
            Assert.That(putResult.RejectReason, Is.Null);

            string entryPath = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).Single();
            string json = File.ReadAllText(entryPath);

            EvaluationResults evaluation = LoadSchema().Evaluate(
                JsonNode.Parse(json), new EvaluationOptions { OutputFormat = OutputFormat.List });

            Assert.That(evaluation.IsValid, Is.True,
                string.Join(Environment.NewLine, evaluation.Details.Where(d => !d.IsValid)
                    .Select(d => d.EvaluationPath + ": " + string.Join(",", d.Errors?.Values ?? []))));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public void LegacyCacheEntry_WithoutWorkProvenance_ValidatesAgainstSchema()
    {
        string cacheRoot = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-schema-legacy", Guid.NewGuid().ToString("N"));
        try
        {
            AnalysisCacheEntryV1 entry = BuildSampleEntry(cacheRoot);
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(entry, AnalysisCacheJson.Options))!.AsObject();
            Assert.That(json.Remove("WorkProvenance"), Is.True);

            EvaluationResults evaluation = LoadSchema().Evaluate(
                json, new EvaluationOptions { OutputFormat = OutputFormat.List });

            Assert.That(evaluation.IsValid, Is.True,
                string.Join(Environment.NewLine, evaluation.Details.Where(d => !d.IsValid)
                    .Select(d => d.EvaluationPath + ": " + string.Join(",", d.Errors?.Values ?? []))));
        }
        finally
        {
            if (Directory.Exists(cacheRoot))
            {
                Directory.Delete(cacheRoot, recursive: true);
            }
        }
    }

    [Test]
    public void PackagedSchemaRegistry_ListsAnalysisCache()
    {
        PackagedSchemaRegistry registry = new();

        Assert.That(registry.List().Select(descriptor => descriptor.LogicalId), Does.Contain("analysis-cache"));
    }
}
