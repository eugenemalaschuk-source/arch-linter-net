using System.Text.Json;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Baseline;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Schema;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class NormalizedFindingCliProducerTests
{
    [Test]
    public void BaselineLifecycleJson_PreservesLegacyIdentityAndValidatesNormalizedEnvelope()
    {
        var identity = new ArchitectureViolationIdentity(
            2, "method_body", "call", "rule", "App", "App.Service", "Run",
            "Infra", "Infra.Client", "Call", 1);
        var entry = new ArchitectureBaselineComparisonEntry(
            "strict_method_body", "rule", "App.Service", "Infra.Client.Call", "debt", identity);

        string json = JsonSerializer.Serialize(
            BaselineLifecycleFormatter.EntryForJson(entry, BaselineEntryLifecycle.Stale));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("identity").GetProperty("sourceAssembly").GetString(), Is.EqualTo("App"));
            Assert.That(root.GetProperty("identity").GetProperty("canonical").GetString(), Is.Not.Empty);
            Assert.That(root.GetProperty("details").GetProperty("contract_group").GetString(), Is.EqualTo("strict_method_body"));
            Assert.That(NormalizedSchema().Evaluate(root).IsValid, Is.True, json);
        });
    }

    [Test]
    public void PolicyErrorJson_ValidatesNormalizedEnvelope()
    {
        string json = PolicyDiagnosticOutputWriter.BuildJsonText(
            "invalid policy",
            new ArchitecturePolicyDiagnostic(
                ArchitecturePolicyDiagnosticKind.SemanticValidation,
                null,
                [],
                []),
            "semantic-validation");
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.That(NormalizedSchema().Evaluate(document.RootElement).IsValid, Is.True, json);
    }

    private static JsonSchema NormalizedSchema()
    {
        PackagedSchemaRegistry registry = new();
        Assert.That(registry.TryRead("normalized-finding", out string schemaText), Is.True);
        return JsonSchema.FromText(schemaText);
    }
}
