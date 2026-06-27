using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDiagnosticFormatterTests
{
    [Test]
    public void FormatViolationsForHumans_DependencyDiagnostic_IncludesLayerContext()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "protected layer 'Core'", new[] { "ref1" })
            {
                SourceLayer = "Web",
                TargetLayer = "Core",
                AllowedImporters = new[] { "Api" }
            }
        };

        string output = ArchitectureDiagnosticFormatter.FormatViolationsForHumans(violations);

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
                DependencyPaths = new IReadOnlyCollection<string>[] { new[] { "Source.Type", "Mid", "Forbidden.Namespace" } }
            }
        };

        string output = ArchitectureDiagnosticFormatter.FormatViolationsForHumans(violations);

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

        string output = ArchitectureDiagnosticFormatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("matched Forbidden.Namespace.Internal"));
    }

    [Test]
    public void FormatResultForCiArtifacts_ConfigurationDiagnostic_IncludesTemplateAndContainerFields()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "Forbidden.Namespace", new[] { "ref1" })
            {
                TemplateName = "asmdef-template",
                ContainerNamespace = "MyApp.Modules"
            }
        };

        string json = ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
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
                SourceLayer = "Web",
                TargetLayer = "Core",
                AllowedImporters = new[] { "Api" },
                MatchedNamespacePrefixes = new[] { "Core.Internal" }
            }
        };

        string json = ArchitectureDiagnosticFormatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        JsonElement violation = doc.RootElement.GetProperty("violations")[0];
        Assert.That(violation.GetProperty("source_layer").GetString(), Is.EqualTo("Web"));
        Assert.That(violation.GetProperty("matched_namespace_prefixes")[0].GetString(), Is.EqualTo("Core.Internal"));
    }

    [Test]
    public void FormatUnmatchedForHumans_NoEntries_ReturnsEmptyString()
    {
        string output = ArchitectureDiagnosticFormatter.FormatUnmatchedForHumans(
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

        string output = ArchitectureDiagnosticFormatter.FormatUnmatchedForHumans(unmatched);

        Assert.That(output, Does.Contain("source_type: Source.Type"));
        Assert.That(output, Does.Contain("forbidden_reference: Forbidden.Ref"));
        Assert.That(output, Does.Contain("reason: stale ignore"));
    }
}
