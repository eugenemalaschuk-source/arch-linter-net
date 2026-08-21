using System.Text.Json;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyWeakeningComparerTests
{
    [Test]
    public void Compare_StrictToSameControlAudit_ReportsSemanticDowngradeWithImportedProvenance()
    {
        ArchitecturePolicyContextExport baseline = Context(contracts: [Contract("strict", "dependency", "layer-boundary", provenance: _importedProvenance)]);
        ArchitecturePolicyContextExport current = Context(contracts: [Contract("audit", "dependency", "layer-boundary")]);

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current)).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("strict_to_audit"));
            Assert.That(finding.Classification, Is.EqualTo("semantic"));
            Assert.That(finding.ControlIdentity, Is.EqualTo("dependency:layer-boundary"));
            Assert.That(finding.BaseProvenance, Is.EqualTo(_importedProvenance));
        });
    }

    [Test]
    public void Compare_UnrelatedStrictAndAuditControls_DoesNotPairThemByDisplayName()
    {
        ArchitecturePolicyContextExport baseline = Context(contracts: [Contract("strict", "dependency", "strict-control")]);
        ArchitecturePolicyContextExport current = Context(contracts: [Contract("audit", "dependency", "unrelated-audit-control")]);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current));

        Assert.Multiple(() =>
        {
            Assert.That(result.Findings.Select(finding => finding.Kind), Does.Contain("strict_control_removed"));
            Assert.That(result.Findings.Select(finding => finding.Kind), Does.Not.Contain("strict_to_audit"));
        });
    }

    [Test]
    public void Compare_RemovedImportedStrictControl_PreservesTheBaseAuthoredProvenance()
    {
        ArchitecturePolicyContextExport baseline = Context(contracts: [Contract("strict", "dependency", "imported-boundary", provenance: _importedProvenance)]);

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, Context())).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("strict_control_removed"));
            Assert.That(finding.BaseProvenance, Is.EqualTo(_importedProvenance));
            Assert.That(finding.CurrentProvenance, Is.Null);
        });
    }

    [Test]
    public void Compare_StaticScopeAndPermissionRelaxation_UsesTypedEffectiveInputs()
    {
        ArchitecturePolicyContextContract baselineContract = Contract(
            "strict",
            "composition",
            "composition-root",
            facts:
            [
                Fact("forbidden_apis", "Legacy.Resolve", "Legacy.Register"),
                Fact("allowed_only_in_assemblies", "Sample.Host"),
                Fact("assemblies", "Sample.Host", "Sample.Worker"),
            ]);
        ArchitecturePolicyContextContract currentContract = Contract(
            "strict",
            "composition",
            "composition-root",
            facts:
            [
                Fact("forbidden_apis", "Legacy.Resolve"),
                Fact("allowed_only_in_assemblies", "Sample.Host", "Sample.Worker"),
                Fact("assemblies", "Sample.Host"),
            ]);
        ArchitecturePolicyContextExport baseline = Context(
            contracts: [baselineContract],
            sourceSets: [new ArchitecturePolicyContextSourceSet("hosts", "explicit", ["Sample.Host", "Sample.Worker"], false, "", _importedProvenance)],
            analysis: Analysis(projects: ["src/Sample.Host/Sample.Host.csproj", "src/Sample.Worker/Sample.Worker.csproj"]));
        ArchitecturePolicyContextExport current = Context(
            contracts: [currentContract],
            sourceSets: [new ArchitecturePolicyContextSourceSet("hosts", "explicit", ["Sample.Host"], false, "", null)],
            analysis: Analysis(projects: ["src/Sample.Host/Sample.Host.csproj"], projectExclude: ["tests/**"]));

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current));

        Assert.That(result.Findings.Select(finding => finding.Kind), Is.EquivalentTo(new[]
        {
            "analysis_project_exclusion_added",
            "analysis_scope_input_removed",
            "permission_broadened",
            "prohibition_removed",
            "scope_inventory_narrowed",
            "source_set_member_removed",
        }));
    }

    [Test]
    public void Compare_NewMatchedSubtractionAndUniversalIgnore_AreSemanticWeakening()
    {
        ArchitecturePolicyContextContract contract = Contract("strict", "assembly_dependency", "core-dependency");
        ArchitecturePolicyContextSourceExpansion baselineExpansion = new(
            "strict_assembly_dependency", "core-dependency", "core dependency", "fan_out", null, [], false, "", null,
            [Expanded("Sample.Core")], [Expanded("Sample.Core")], []);
        ArchitecturePolicyContextSourceExpansion currentExpansion = baselineExpansion with
        {
            Instances = [],
            Exclusions = [new ArchitecturePolicyContextExpandedExclusion("Sample.Core", null, null, true, false, "", _importedProvenance)],
        };
        ArchitecturePolicyContextExport baseline = Context(contracts: [contract], expansions: [baselineExpansion]);
        ArchitecturePolicyContextExport current = Context(
            contracts: [contract],
            expansions: [currentExpansion],
            exceptions: [new ArchitecturePolicyContextException("contract", "core-dependency", "ignored_violation", "* | *", "Temporary migration")]);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current));

        Assert.That(result.Findings.Select(finding => finding.Kind), Is.EquivalentTo(new[]
        {
            "effective_source_removed",
            "source_exclusion_added",
            "universal_exception_added",
        }));
        Assert.That(result.Findings.All(finding => finding.Classification == "semantic"), Is.True);
    }

    [Test]
    public void Compare_NewNarrowIgnoredViolation_IsNotClassifiedAsBroadWeakening()
    {
        ArchitecturePolicyContextContract contract = Contract("strict", "dependency", "bounded-boundary");
        ArchitecturePolicyContextExport current = Context(
            contracts: [contract],
            exceptions: [new ArchitecturePolicyContextException(
                "contract", "bounded-boundary", "ignored_violation", "Sample.Legacy.Type | Sample.Infrastructure.LegacyGateway", "Tracked in #119")]);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(Context(contracts: [contract]), current));

        Assert.That(result.Findings, Is.Empty);
    }

    [Test]
    public void Compare_EquivalentEffectiveValuesWithDifferentOrdering_IsNoOp()
    {
        ArchitecturePolicyContextExport baseline = Context(
            contracts:
            [
                Contract("strict", "dependency", "first", [Fact("forbidden", "Sample.A", "Sample.B")]),
                Contract("strict", "dependency", "second"),
            ],
            sourceSets: [new ArchitecturePolicyContextSourceSet("sources", "explicit", ["Sample.A", "Sample.B"], false, "", null)]);
        ArchitecturePolicyContextExport current = Context(
            contracts:
            [
                Contract("strict", "dependency", "second"),
                Contract("strict", "dependency", "first", [Fact("forbidden", "Sample.B", "Sample.A")]),
            ],
            sourceSets: [new ArchitecturePolicyContextSourceSet("sources", "explicit", ["Sample.B", "Sample.A"], false, "", null)]);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current));

        Assert.That(result.Findings, Is.Empty);
    }

    [Test]
    public void Compare_SelectorChangeWithoutMembershipEvidence_IsBoundedAndDoesNotInventSubjects()
    {
        ArchitecturePolicyContextExport baseline = Context(contracts: [PublicApiContract("Api", "DomainRole")]);
        ArchitecturePolicyContextExport current = Context(contracts: [PublicApiContract("Api", "ApplicationRole")]);

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current)).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("selector_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.AffectedSubjects, Is.Empty);
        });
    }

    [Test]
    public void Compare_SelectorChangeWithMatchingCompleteMembershipEvidence_ReportsExactRemovedSubjects()
    {
        ArchitecturePolicyContextExport baseline = Context(contracts: [PublicApiContract("Api", "DomainRole")]);
        ArchitecturePolicyContextExport current = Context(contracts: [PublicApiContract("Api", "ApplicationRole")]);
        ArchitecturePolicyMembershipEvidence baseMembership = Membership(baseline, ["Sample.Api:Sample.Api.LegacyContract"]);
        ArchitecturePolicyMembershipEvidence currentMembership = Membership(current, []);

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(
            new(baseline, current, baseMembership, currentMembership)).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("selector_scope_reduced"));
            Assert.That(finding.Classification, Is.EqualTo("semantic"));
            Assert.That(finding.AffectedSubjects, Is.EqualTo(new[] { "Sample.Api:Sample.Api.LegacyContract" }));
        });
    }

    [Test]
    public void Formatters_UseTheSameNormalizedIdentityAndSeverity()
    {
        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [Contract("strict", "dependency", "boundary")]),
            Context(contracts: [Contract("audit", "dependency", "boundary")], severity: "warn")));

        string human = ArchitecturePolicyWeakeningFormatter.FormatAsHuman(result);
        using JsonDocument json = JsonDocument.Parse(ArchitecturePolicyWeakeningFormatter.FormatAsJson(result));
        using JsonDocument sarif = JsonDocument.Parse(ArchitecturePolicyWeakeningFormatter.FormatAsSarif(result));
        string identity = result.Findings.Single().Identity;

        Assert.Multiple(() =>
        {
            Assert.That(result.HasErrors, Is.False);
            Assert.That(human, Does.Contain("[warn] [semantic] [strict_to_audit]"));
            Assert.That(json.RootElement.GetProperty("findings")[0].GetProperty("identity").GetString(), Is.EqualTo(identity));
            Assert.That(sarif.RootElement.GetProperty("runs")[0].GetProperty("results")[0].GetProperty("properties")
                .GetProperty("identity").GetString(), Is.EqualTo(identity));
        });
    }

    [Test]
    public void DeserializeContext_IncompleteArtifactFailsClosed()
    {
        Assert.That(() => ArchitecturePolicyWeakeningFormatter.DeserializeContext("{}"), Throws.ArgumentException);
    }

    private static ArchitecturePolicyContextExport Context(
        IReadOnlyList<ArchitecturePolicyContextContract>? contracts = null,
        IReadOnlyList<ArchitecturePolicyContextSourceSet>? sourceSets = null,
        IReadOnlyList<ArchitecturePolicyContextSourceExpansion>? expansions = null,
        IReadOnlyList<ArchitecturePolicyContextException>? exceptions = null,
        ArchitecturePolicyContextAnalysis? analysis = null,
        string severity = "error") => new(
        ArchitecturePolicyContextExport.CurrentSchemaVersion,
        "architecture-policy-context",
        new ArchitecturePolicyContextPolicy("Sample policy", 1, "architecture/policy.yml", false),
        new ArchitecturePolicyContextGuardrails(severity),
        analysis ?? Analysis(),
        [new ArchitecturePolicyContextSource("architecture/policy.yml", "root", 0, null, null, [])],
        [],
        contracts ?? [],
        [],
        [],
        [],
        sourceSets ?? [],
        expansions ?? [],
        exceptions ?? [],
        []);

    private static ArchitecturePolicyContextAnalysis Analysis(
        IReadOnlyList<string>? projects = null,
        IReadOnlyList<string>? projectExclude = null) => new([], projects ?? [], [], projectExclude ?? [], []);

    private static ArchitecturePolicyContextContract Contract(
        string mode,
        string family,
        string id,
        IReadOnlyList<ArchitecturePolicyContextContractFact>? facts = null,
        ArchitecturePolicyContextProvenance? provenance = null) => new(
        mode,
        family,
        id,
        id,
        null,
        "Reviewed migration rationale",
        [],
        facts ?? [],
        [],
        [],
        [],
        [],
        provenance);

    private static ArchitecturePolicyContextContract PublicApiContract(string id, string role) => Contract(
        "strict",
        "public_api_surface",
        id,
        [new ArchitecturePolicyContextContractFact("surface_selector", [], [Fact("role", role)])]);

    private static ArchitecturePolicyContextContractFact Fact(string name, params string[] values) => new(name, values, []);

    private static ArchitecturePolicyContextExpandedInstance Expanded(string source) => new(
        "core-dependency", source, null, null, false, "", null, null, null);

    private static ArchitecturePolicyMembershipEvidence Membership(ArchitecturePolicyContextExport context, IReadOnlyList<string> subjects) => new(
        ArchitecturePolicyMembershipEvidence.CurrentSchemaVersion,
        ArchitecturePolicyMembershipEvidence.EvidenceKind,
        ArchitecturePolicyWeakeningFormatter.ComputeContextDigest(context),
        true,
        [new ArchitecturePolicyContractMembership("public_api_surface", "Api", subjects)]);

    private static readonly ArchitecturePolicyContextProvenance _importedProvenance = new(
        "architecture/policy/contracts.yml", "architecture/policy.yml", "fragment", "contracts.strict[0]", 1);
}
