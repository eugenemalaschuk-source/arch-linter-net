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
    public void FormatDiagnostics_AllPayloadKinds_IncludesHumanAndCiContext()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("type", "type-id", "Type.Source", "Type.Forbidden", new[] { "Type.Ref" })
            { Payload = new TypePlacementPayload("Expected.Location", "Actual.Location", "Expected.Name", "Actual.Name") },
            new("api", "api-id", "Api.Source", "Api.Forbidden", new[] { "Api.Ref" })
            { Payload = new PublicApiSurfacePayload("public void Api()", true, "Api.Assembly", "public") },
            new("attribute", "attribute-id", "Attribute.Source", "Attribute.Forbidden", new[] { "Attribute.Ref" })
            { Payload = new AttributeUsagePayload("ObsoleteAttribute", "forbidden", "Expected.Attribute", "Actual.Attribute") },
            new("inheritance", "inheritance-id", "Inheritance.Source", "Inheritance.Forbidden", new[] { "Inheritance.Ref" })
            { Payload = new InheritancePayload("Forbidden.Base", "public_api") },
            new("interface", "interface-id", "Interface.Source", "Interface.Forbidden", new[] { "Interface.Ref" })
            { Payload = new InterfaceImplementationPayload("IForbidden", "missing", "Expected.Interface", "Actual.Interface") },
            new("composition", "composition-id", "Composition.Source", "Composition.Forbidden", new[] { "Composition.Ref" })
            { Payload = new CompositionPayload("Composition.Configure", "Forbidden.Api", "Composition boundary") },
            new("project", "project-id", "Project.Source", "Project.Forbidden", new[] { "Project.Ref" })
            { Payload = new ProjectMetadataPayload("forbidden_property", "Nullable", "enable", "disable", "src/App.csproj") },
            new("external", "external-id", "External.Source", "External.Forbidden", new[] { "External.Ref" })
            { Payload = new ExternalDependencyPayload("vendor_sdk") }
        };

        string human = _formatter.FormatViolationsForHumans(violations);
        Assert.That(human, Does.Contain("expected_location: Expected.Location"));
        Assert.That(human, Does.Contain("reason: forbidden_public_constant"));
        Assert.That(human, Does.Contain("attribute: ObsoleteAttribute"));
        Assert.That(human, Does.Contain("forbidden_base_type: Forbidden.Base"));
        Assert.That(human, Does.Contain("interface: IForbidden"));
        Assert.That(human, Does.Contain("matched_api: Forbidden.Api"));
        Assert.That(human, Does.Contain("source_path: src/App.csproj"));
        Assert.That(human, Does.Contain("external_group: vendor_sdk"));

        using var document = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>()));
        JsonElement serialized = document.RootElement.GetProperty("violations");
        Assert.That(serialized.ToString(), Does.Contain("expected_type_location"));
        Assert.That(serialized.ToString(), Does.Contain("undeclared_api_signature"));
        Assert.That(serialized.ToString(), Does.Contain("matched_attribute"));
        Assert.That(serialized.ToString(), Does.Contain("forbidden_base_type"));
        Assert.That(serialized.ToString(), Does.Contain("matched_interface"));
        Assert.That(serialized.ToString(), Does.Contain("project_metadata_source_path"));
        Assert.That(serialized.ToString(), Does.Contain("forbidden_external_group"));
    }

    [Test]
    public void FormatCoverageAndPolicyResults_IncludesSortedDetails()
    {
        var summary = new ArchitectureCoverageSummary(
            "coverage", "coverage-id", "namespace",
            new ArchitectureCoverageSummaryCounts(1, 1, 1, 1, 1),
            new[] { new ArchitectureCoverageSummaryExcludedItem("z-excluded", "generated") },
            new[] { new ArchitectureCoverageSummaryEvidenceItem("a-uncovered", "a-evidence") },
            new[] { new ArchitectureCoverageSummaryEvidenceItem("b-stale", "b-evidence") },
            new[] { new ArchitectureCoverageSummaryEvidenceItem("c-unknown", "c-evidence") },
            new[] { new ArchitectureCoverageSummaryEvidenceItem("d-covered", "d-evidence") });
        var policy = new PolicyConsistencyDiagnostic(
            "policy", "policy-id", "duplicate", "conflicting rules",
            new[] { "first-id" }, new[] { "first", "second" }, new[] { "Core" })
        { RepresentativeType = "Core.Representative" };

        Assert.That(_formatter.FormatCoverageForHumans(new[]
        {
            new ArchitectureViolation("coverage", "coverage-id", "Source", "Forbidden", new[] { "Reference" })
        }), Does.StartWith("Coverage findings:"));
        string humanSummary = _formatter.FormatCoverageSummaryForHumans(new[] { summary });
        Assert.That(humanSummary, Does.Contain("covered=1 excluded=1 uncovered=1 stale=1 unknown=1"));
        Assert.That(humanSummary, Does.Contain("uncovered: a-uncovered (a-evidence)"));
        Assert.That(_formatter.FormatPolicyConsistencyForHumans(new[] { policy }), Does.Contain("Core.Representative").Or.Contain("conflicting rules"));

        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            policyConsistencyFindings: new[] { policy }, coverageSummaries: new[] { summary }));
        Assert.That(json.RootElement.GetProperty("policy_consistency_findings")[0].GetProperty("representative_type").GetString(),
            Is.EqualTo("Core.Representative"));
        Assert.That(json.RootElement.GetProperty("coverage_summary")[0].GetProperty("covered_items")[0].GetProperty("item").GetString(),
            Is.EqualTo("d-covered"));
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
