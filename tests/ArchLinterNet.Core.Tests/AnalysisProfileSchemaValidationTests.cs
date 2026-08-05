using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Schema;
using ArchLinterNet.Testing;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// A real generated profile validates against the exact release-matched resource returned by
// PackagedSchemaRegistry. See openspec/specs/analysis-profile/spec.md.
[TestFixture]
public sealed class AnalysisProfileSchemaValidationTests
{
    private static JsonSchema LoadSchema()
    {
        PackagedSchemaRegistry registry = new();
        Assert.That(registry.TryRead("analysis-profile", out string schemaText), Is.True);
        return JsonSchema.FromText(schemaText);
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
    public void PackagedSchemaRegistry_ListsAnalysisProfile()
    {
        PackagedSchemaRegistry registry = new();

        Assert.That(registry.List().Select(descriptor => descriptor.LogicalId), Does.Contain("analysis-profile"));
    }
}
