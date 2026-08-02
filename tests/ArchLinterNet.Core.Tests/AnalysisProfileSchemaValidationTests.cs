using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Schema;
using ArchLinterNet.Testing;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #374: a real generated profile validates against schema/0.5.1/analysis-profile.schema.json,
// which is NOT yet registered in the packaged schema registry — registration is #410's scope.
// See openspec/specs/analysis-profile/spec.md and openspec/specs/packaged-schema-registry/spec.md.
[TestFixture]
public sealed class AnalysisProfileSchemaValidationTests
{
    private static JsonSchema LoadSchema()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string schemaPath = Path.Combine(repositoryRoot, "schema", "0.5.1", "analysis-profile.schema.json");
        return JsonSchema.FromText(File.ReadAllText(schemaPath));
    }

    private static string WriteHarmlessPolicy()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-analysis-profile-schema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test

            layers:
              execution:
                namespace: ArchLinterNet.Core.Execution

            analysis:
              target_assemblies: [ArchLinterNet.Core]
            """);
        return policyPath;
    }

    [Test]
    public void RealGeneratedProfile_ValidatesAgainstSchema()
    {
        var builder = new ArchitectureValidationBuilder(WriteHarmlessPolicy()).WithProfile();
        ArchitectureValidationResult result = builder.ValidateStrict();

        Assert.That(result.Profile, Is.Not.Null);
        string json = AnalysisProfileJsonWriter.Write(result.Profile!);

        EvaluationResults evaluation = LoadSchema().Evaluate(
            System.Text.Json.Nodes.JsonNode.Parse(json), new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.That(evaluation.IsValid, Is.True,
            string.Join(Environment.NewLine, evaluation.Details.Where(d => !d.IsValid).Select(d => d.EvaluationPath + ": " + string.Join(",", d.Errors?.Values ?? []))));
    }

    [Test]
    public void PackagedSchemaRegistry_DoesNotYetListAnalysisProfile()
    {
        PackagedSchemaRegistry registry = new();

        Assert.That(registry.List().Select(descriptor => descriptor.LogicalId), Does.Not.Contain("analysis-profile"));
    }
}
