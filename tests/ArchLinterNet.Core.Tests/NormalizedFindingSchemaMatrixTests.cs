using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Schema;
using Json.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class NormalizedFindingSchemaMatrixTests
{
    [Test]
    public void EveryFindingFamily_CentralProjectionValidatesAgainstPackagedV2Schema()
    {
        PackagedSchemaRegistry registry = new();
        Assert.That(registry.TryRead("normalized-finding", out string schemaText), Is.True);
        JsonSchema schema = JsonSchema.FromText(schemaText);

        ArchitectureFinding[] findings = OrdinaryViolations()
            .Select(violation => ArchitectureFindingMapper.FromViolation(violation, "strict"))
            .Concat(SpecialFindings())
            .ToArray();

        Assert.That(
            findings.Select(finding => finding.Kind),
            Is.EquivalentTo(Enum.GetValues<ArchitectureDiagnosticKind>()
                .Select(ArchitectureFindingMapper.KindToken)));

        foreach (ArchitectureFinding finding in findings)
        {
            string json = JsonSerializer.Serialize(
                ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding));
            using JsonDocument document = JsonDocument.Parse(json);
            EvaluationResults result = schema.Evaluate(document.RootElement);
            Assert.That(result.IsValid, Is.True, $"Schema rejected generated '{finding.Kind}' finding: {json}");
        }
    }

    private static IEnumerable<ArchitectureViolation> OrdinaryViolations()
    {
        yield return Violation(new DependencyPayload("source", "target"));
        yield return Violation(new ConfigurationPayload("template"));
        yield return Violation(new ExternalDependencyPayload("vendor"));
        yield return Violation(new PackageDependencyPayload("forbidden"));
        yield return Violation(new PackageAllowOnlyPayload(["allowed"]));
        yield return Violation(new MetricBudgetPayload(
            "component-budget", "component-count", "topology_type_count", "App.Core", "core",
            4, "maximum", 3, ["App.Core.A", "App.Core.B", "App.Core.C", "App.Core.D"]));
        yield return Violation(new FrameworkReferencePayload("aspnet"));
        yield return Violation(new FrameworkReferenceAllowOnlyPayload(["runtime"]));
        yield return Violation(new TypePlacementPayload("expected", "actual", "Expected", "Actual"));
        yield return Violation(new LayoutConventionPayload("src/App.cs", "class", "interface"));
        yield return Violation(new PublicApiSurfacePayload(
            "public void Added()", false, "Product", "public", "added", null));
        yield return Violation(new AttributeUsagePayload("ObsoleteAttribute", "forbidden"));
        yield return Violation(new InheritancePayload("Forbidden.Base", "public_api"));
        yield return Violation(new InterfaceImplementationPayload("IForbidden", "implemented"));
        yield return Violation(new CompositionPayload("Configure", "Forbidden.Api", "Product"));
        yield return Violation(new ProjectMetadataPayload("property", "Nullable", "enable", "disable", "src/App.csproj"));
        yield return Violation(new ContextDependencyPayload("Domain", null, "Infrastructure", null, "forbidden"));
        yield return Violation(new ContextAllowOnlyPayload("Domain", null, "Domain", null, "none"));
        yield return Violation(new PortBoundaryPayload(
            "Adapter", null, "Port", null, "direct-reference", "IOrderPort", "depend on the port"));
    }

    private static IEnumerable<ArchitectureFinding> SpecialFindings()
    {
        yield return ArchitectureFindingMapper.FromDiagnostic(
            new CycleDiagnostic("cycle", "cycle-id", "A -> B -> A"), "strict");
        yield return ArchitectureFindingMapper.FromDiagnostic(
            new UnmatchedIgnoreDiagnostic("ignore", "ignore-id", 0, "Source", "Target", "stale"), "strict");
        yield return ArchitectureFindingMapper.FromDiagnostic(
            new PolicyConsistencyDiagnostic(
                "policy", "policy-id", "duplicate-id", "duplicate", ["a"], ["A"], ["layer"]), "strict");
        yield return ArchitectureFindingMapper.FromDiagnostic(
            new BuildStatePreflightDiagnostic(
                "preflight", "preflight-id", BuildStatePreflightState.MissingArtifact,
                new BuildStatePreflightEvidence("src/App.csproj", "App")), "strict");
        yield return ArchitectureFindingMapper.FromBaseline(new BaselineLifecycleEntry(
            new ArchitectureBaselineComparisonEntry(
                "strict", "baseline-id", "Source", "Target", "debt"),
            BaselineEntryLifecycle.Stale));
        yield return ArchitectureFindingMapper.FromPolicyError(
            "invalid policy",
            new ArchitecturePolicyDiagnostic(
                ArchitecturePolicyDiagnosticKind.SemanticValidation,
                null,
                [],
                []),
            "semantic-validation");
        yield return ArchitectureFindingMapper.FromApplicabilityDiagnostic(
            new ArchitectureApplicabilityDiagnostic(
                "applicability-control",
                "future-family",
                ArchitectureApplicabilityMembership.Required,
                ArchitectureApplicabilityRecordState.Unassessable,
                ArchitectureApplicabilityRecordState.Unassessable,
                new ArchitectureApplicabilityReason(
                    "family_specific_unavailable",
                    "future-family",
                    "applicability-control",
                    "policy-id")),
            "strict");
    }

    private static ArchitectureViolation Violation(IArchitectureDiagnosticPayload payload) =>
        new("contract", "contract-id", "Product.Source", "Product.Forbidden", ["Product.Target"])
        {
            Payload = payload,
        };
}
