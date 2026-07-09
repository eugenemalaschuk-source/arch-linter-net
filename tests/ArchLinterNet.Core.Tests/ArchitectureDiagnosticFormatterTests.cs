using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDiagnosticFormatterTests
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();

    [Test]
    public void FormatViolationsForHumans_DependencyDiagnostic_IncludesLayerContext()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "protected layer 'Core'", new[] { "ref1" })
            {
                Payload = new DependencyPayload(
                    SourceLayer: "Web",
                    TargetLayer: "Core",
                    AllowedImporters: new[] { "Api" })
            }
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("source_layer: Web"));
        Assert.That(output, Does.Contain("target_layer: Core"));
        Assert.That(output, Does.Contain("allowed_importers: [Api]"));
    }

    [Test]
    public void FormatViolationsForHumans_ConfigurationDiagnosticWithDependencyPaths_IncludesViaLines()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "Forbidden.Namespace", new[] { "ref1" })
            {
                Payload = new ConfigurationPayload(
                    DependencyPaths: new IReadOnlyCollection<string>[] { new[] { "Source.Type", "Mid", "Forbidden.Namespace" } })
            }
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("via: Source.Type -> Mid -> Forbidden.Namespace"));
    }

    [Test]
    public void FormatViolationsForHumans_MatchedNamespacePrefix_AnnotatesNamespace()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "Forbidden.Namespace", new[] { "ref1" })
            {
                MatchedNamespacePrefixes = new[] { "Forbidden.Namespace.Internal" }
            }
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("matched Forbidden.Namespace.Internal"));
    }

    [Test]
    public void FormatResultForCiArtifacts_ConfigurationDiagnostic_IncludesTemplateAndContainerFields()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "Forbidden.Namespace", new[] { "ref1" })
            {
                Payload = new ConfigurationPayload(
                    TemplateName: "asmdef-template",
                    ContainerNamespace: "MyApp.Modules")
            }
        };

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        JsonElement violation = doc.RootElement.GetProperty("violations")[0];
        Assert.That(violation.GetProperty("template_name").GetString(), Is.EqualTo("asmdef-template"));
        Assert.That(violation.GetProperty("container_namespace").GetString(), Is.EqualTo("MyApp.Modules"));
    }

    [Test]
    public void FormatResultForCiArtifacts_LayerDiagnosticWithMatchedPrefixes_IncludesBothFieldGroups()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "protected layer 'Core'", new[] { "ref1" })
            {
                Payload = new DependencyPayload(
                    SourceLayer: "Web",
                    TargetLayer: "Core",
                    AllowedImporters: new[] { "Api" }),
                MatchedNamespacePrefixes = new[] { "Core.Internal" }
            }
        };

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        JsonElement violation = doc.RootElement.GetProperty("violations")[0];
        Assert.That(violation.GetProperty("source_layer").GetString(), Is.EqualTo("Web"));
        Assert.That(violation.GetProperty("matched_namespace_prefixes")[0].GetString(), Is.EqualTo("Core.Internal"));
    }

    [Test]
    public void FormatViolationsForHumans_CompositionDiagnostic_IncludesSourceMember()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("composition", null, "Source.Type", "Forbidden.Api", new[] { "Forbidden.Api" })
            {
                Payload = new CompositionPayload(
                    SourceMember: "Source.Type.Configure",
                    MatchedForbiddenApi: "Forbidden.Api",
                    ExpectedCompositionBoundary: "namespaces: [Composition]")
            }
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("source_member: Source.Type.Configure"));
    }

    [Test]
    public void FormatResultForCiArtifacts_CompositionDiagnostic_IncludesSourceMember()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("composition", null, "Source.Type", "Forbidden.Api", new[] { "Forbidden.Api" })
            {
                Payload = new CompositionPayload(
                    SourceMember: "Source.Type.Configure",
                    MatchedForbiddenApi: "Forbidden.Api")
            }
        };

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        JsonElement violation = doc.RootElement.GetProperty("violations")[0];
        Assert.That(violation.GetProperty("source_member").GetString(), Is.EqualTo("Source.Type.Configure"));
    }

    [Test]
    public void FormatCyclesForHumans_MultipleCycles_SortedAlphabetically()
    {
        var cycles = new[] { "Z -> Y -> Z", "A -> B -> A" };

        string output = _formatter.FormatCyclesForHumans(cycles);

        Assert.That(output, Is.EqualTo("- A -> B -> A" + Environment.NewLine + "- Z -> Y -> Z"));
    }

    [Test]
    public void FormatCyclesForCiArtifacts_IncludesCyclePaths()
    {
        var cycles = new[] { "A -> B -> A" };

        string json = _formatter.FormatCyclesForCiArtifacts("cycle-contract", "cycle-check", cycles);

        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetProperty("cycles")[0].GetString(), Is.EqualTo("A -> B -> A"));
        Assert.That(doc.RootElement.GetProperty("contract_id").GetString(), Is.EqualTo("cycle-check"));
    }

    [Test]
    public void FormatUnmatchedForHumans_NoEntries_ReturnsEmptyString()
    {
        string output = _formatter.FormatUnmatchedForHumans(
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>());

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void FormatUnmatchedForHumans_Entry_IncludesReasonAndSourceType()
    {
        var unmatched = new List<ArchitectureUnmatchedIgnoredViolation>
        {
            new("contract", "contract-id", 0, "Source.Type", "Forbidden.Ref", "stale ignore")
        };

        string output = _formatter.FormatUnmatchedForHumans(unmatched);

        Assert.That(output, Does.Contain("source_type: Source.Type"));
        Assert.That(output, Does.Contain("forbidden_reference: Forbidden.Ref"));
        Assert.That(output, Does.Contain("reason: stale ignore"));
    }
}
