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
    public void Compare_StaticScopeAndPredicateChanges_UsesBoundedTypedEvidence()
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
            "analysis_project_exclude_impact_not_proven",
            "analysis_projects_impact_not_proven",
            "scope_inventory_narrowed",
            "source_set_member_removed",
            "typed_fact_impact_not_proven",
            "typed_fact_impact_not_proven",
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
            exceptions:
            [
                new ArchitecturePolicyContextException("contract", "core-dependency", "ignored_violation", "*; *", "Temporary migration")
                {
                    IgnoredViolation = new ArchitecturePolicyContextIgnoredViolation("*", "*"),
                },
            ]);

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
            exceptions:
            [
                new ArchitecturePolicyContextException(
                    "contract", "bounded-boundary", "ignored_violation", "Sample.Legacy.Type; Sample.Infrastructure.LegacyGateway", "Tracked in #119")
                {
                    IgnoredViolation = new ArchitecturePolicyContextIgnoredViolation(
                        "Sample.Legacy.Type",
                        "Sample.Infrastructure.LegacyGateway"),
                },
            ]);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(Context(contracts: [contract]), current));

        Assert.That(result.Findings, Is.Empty);
    }

    [Test]
    public void Compare_SourceSetMadeOptionalWithUnchangedMembers_IsSemanticWeakening()
    {
        ArchitecturePolicyContextSourceSet required = new("hosts", "explicit", ["Sample.Host"], false, "", _importedProvenance);
        ArchitecturePolicyContextSourceSet optional = required with { Optional = true, Reason = "Future extraction" };

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(sourceSets: [required]),
            Context(sourceSets: [optional]))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("source_set_made_optional"));
            Assert.That(finding.Classification, Is.EqualTo("semantic"));
            Assert.That(finding.BaseValues, Is.EqualTo(new[] { "required" }));
            Assert.That(finding.CurrentValues, Is.EqualTo(new[] { "optional" }));
        });
    }

    [Test]
    public void Compare_SourceExpansionMadeEmptyTolerant_IsSemanticWeakening()
    {
        ArchitecturePolicyContextSourceExpansion required = new(
            "strict_dependency", "boundary", "boundary", "fan_out", null, [], false, "", _importedProvenance,
            [Expanded("Sample.Application")], [Expanded("Sample.Application")], []);
        ArchitecturePolicyContextSourceExpansion optional = required with { OptionalEmpty = true, OptionalReason = "Planned split" };

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(expansions: [required]),
            Context(expansions: [optional]))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("source_expansion_made_empty_tolerant"));
            Assert.That(finding.Classification, Is.EqualTo("semantic"));
            Assert.That(finding.BaseValues, Is.EqualTo(new[] { "required" }));
            Assert.That(finding.CurrentValues, Is.EqualTo(new[] { "optional_empty" }));
        });
    }

    [Test]
    public void Compare_ProjectGlobChanges_AreBoundedWithoutResolvedProjectMembership()
    {
        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(analysis: Analysis(projectInclude: ["src/Core/**"], projectExclude: ["tests/**"])),
            Context(analysis: Analysis(projectInclude: ["src/**"], projectExclude: ["tests/Fixtures/**"]))));

        Assert.Multiple(() =>
        {
            Assert.That(result.Findings.Select(finding => finding.Kind), Is.EquivalentTo(new[]
            {
                "analysis_project_exclude_impact_not_proven",
                "analysis_project_include_impact_not_proven",
            }));
            Assert.That(result.Findings.All(finding => finding.Classification == "impact_not_proven"), Is.True);
        });
    }

    [TestCase("src/Core", "src")]
    [TestCase("src", "")]
    public void Compare_SourceRootChange_IsImpactNotProvenWithoutEffectiveScopeEvidence(string baselineRoot, string currentRoot)
    {
        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(analysis: Analysis(sourceRoots: [baselineRoot])),
            Context(analysis: Analysis(sourceRoots: string.IsNullOrEmpty(currentRoot) ? [] : [currentRoot])))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("analysis_source_roots_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.AffectedSubjects, Is.Empty);
        });
    }

    [Test]
    public void Compare_TargetAssembliesChangedToEmptyAuthoredList_IsImpactNotProvenWithoutEffectiveScopeEvidence()
    {
        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(analysis: Analysis(targetAssemblies: ["Sample.Host"])),
            Context(analysis: Analysis()))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("analysis_target_assemblies_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.AffectedSubjects, Is.Empty);
        });
    }

    [Test]
    public void Compare_RequiredTemplateLayerAndCoverageInputMadeOptional_AreSemanticWeakening()
    {
        ArchitecturePolicyContextContract baseline = Contract("strict", "coverage", "coverage", [
            FactItems("layers", FactItems("layer", Fact("name", "Application"), Fact("optional", "false"))),
        ]);
        ArchitecturePolicyContextContract current = Contract("strict", "coverage", "coverage", [
            FactItems("layers", FactItems("layer", Fact("name", "Application"), Fact("optional", "true"))),
            FactItems("optional_inputs", FactItems("input", Fact("contract_id", "coverage"), Fact("input", "roots"), Fact("layer", "Application"))),
        ]);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [baseline]),
            Context(contracts: [current])));

        Assert.That(result.Findings.Select(finding => finding.Kind), Is.EquivalentTo(new[]
        {
            "required_input_made_optional",
            "required_layer_made_optional",
        }));
    }

    [Test]
    public void Compare_ChangedUnsupportedTypedFact_IsImpactNotProven()
    {
        ArchitecturePolicyContextContract baseline = Contract("strict", "dependency", "boundary", [Fact("unmodeled_constraint", "one")]);
        ArchitecturePolicyContextContract current = Contract("strict", "dependency", "boundary", [Fact("unmodeled_constraint", "two")]);

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [baseline]),
            Context(contracts: [current]))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("typed_fact_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.AffectedSubjects, Is.Empty);
        });
    }

    [Test]
    public void Compare_ForbiddenLegacyRuntimeStrengthening_IsNotWeakening()
    {
        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [Contract("strict", "dependency", "boundary", [Fact("forbidden_legacy_runtime", "false")])]),
            Context(contracts: [Contract("strict", "dependency", "boundary", [Fact("forbidden_legacy_runtime", "true")])])));

        Assert.That(result.Findings, Is.Empty);
    }

    [Test]
    public void Compare_ForbiddenLegacyRuntimeRelaxation_IsSemanticWeakening()
    {
        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [Contract("strict", "dependency", "boundary", [Fact("forbidden_legacy_runtime", "true")])]),
            Context(contracts: [Contract("strict", "dependency", "boundary", [Fact("forbidden_legacy_runtime", "false")])]))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("prohibition_removed"));
            Assert.That(finding.Classification, Is.EqualTo("semantic"));
            Assert.That(finding.BaseValues, Is.EqualTo(new[] { "true" }));
            Assert.That(finding.CurrentValues, Is.EqualTo(new[] { "false" }));
        });
    }

    [Test]
    public void Compare_ForbiddenNameSuffixChange_IsImpactNotProven()
    {
        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [Contract("strict", "type_placement", "placement", [Fact("forbidden_name_suffix", "Legacy")])]),
            Context(contracts: [Contract("strict", "type_placement", "placement", [Fact("forbidden_name_suffix", "Deprecated")])]))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("typed_fact_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.AffectedSubjects, Is.Empty);
        });
    }

    [Test]
    public void Compare_ScalarLayerInventoryChange_IsImpactNotProven()
    {
        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [Contract("strict", "cycle", "cycle", [Fact("layers", "Application")])]),
            Context(contracts: [Contract("strict", "cycle", "cycle", [Fact("layers", "Infrastructure")])]))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("typed_fact_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.AffectedSubjects, Is.Empty);
        });
    }

    [Test]
    public void Compare_StructuredAllowedOnlyInTypesExpansion_IsImpactNotProven()
    {
        ArchitecturePolicyContextContract baseline = Contract("strict", "composition", "composition", [
            FactItems("allowed_only_in_types", FactItems("type", Fact("assembly", "Sample.Host"), Fact("type", "Sample.Program"))),
        ]);
        ArchitecturePolicyContextContract current = Contract("strict", "composition", "composition", [
            FactItems("allowed_only_in_types",
                FactItems("type", Fact("assembly", "Sample.Host"), Fact("type", "Sample.Program")),
                FactItems("type", Fact("assembly", "Sample.Host"), Fact("type", "Sample.Bootstrap"))),
        ]);

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [baseline]),
            Context(contracts: [current]))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("typed_fact_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.AffectedSubjects, Is.Empty);
        });
    }

    [TestCase("UnityEngine.UI.", "UnityEngine.")]
    [TestCase("UnityEngine.", "UnityEngine.UI.")]
    public void Compare_ForbiddenBaseTypePrefixChange_IsImpactNotProven(string baselinePrefix, string currentPrefix)
    {
        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [Contract("strict", "inheritance", "inheritance", [Fact("forbidden_base_type_prefixes", baselinePrefix)])]),
            Context(contracts: [Contract("strict", "inheritance", "inheritance", [Fact("forbidden_base_type_prefixes", currentPrefix)])]))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("typed_fact_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.AffectedSubjects, Is.Empty);
        });
    }

    [Test]
    public void Compare_AllowedOnlyInNamespacesGlobChange_IsImpactNotProven()
    {
        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [Contract("strict", "composition", "composition", [Fact("allowed_only_in_namespaces", "Sample.Application.*")])]),
            Context(contracts: [Contract("strict", "composition", "composition", [Fact("allowed_only_in_namespaces", "Sample.Application.Api")])]))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("typed_fact_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.AffectedSubjects, Is.Empty);
        });
    }

    [Test]
    public void Compare_AddedProjectAllowanceWithAllowedAssembly_IsImpactNotProven()
    {
        ArchitecturePolicyContextContract baseline = Contract("strict", "composition", "composition", [
            Fact("allowed_only_in_assemblies", "Sample.Host"),
        ]);
        ArchitecturePolicyContextContract current = Contract("strict", "composition", "composition", [
            Fact("allowed_only_in_assemblies", "Sample.Host"),
            Fact("allowed_only_in_projects", "src/Sample.Host/Sample.Host.csproj"),
        ]);

        ArchitecturePolicyWeakeningFinding finding = ArchitecturePolicyWeakeningComparer.Compare(new(
            Context(contracts: [baseline]),
            Context(contracts: [current]))).Findings.Single();

        Assert.Multiple(() =>
        {
            Assert.That(finding.Kind, Is.EqualTo("typed_fact_impact_not_proven"));
            Assert.That(finding.Classification, Is.EqualTo("impact_not_proven"));
            Assert.That(finding.AffectedSubjects, Is.Empty);
        });
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
        IReadOnlyList<string>? targetAssemblies = null,
        IReadOnlyList<string>? projects = null,
        IReadOnlyList<string>? projectInclude = null,
        IReadOnlyList<string>? projectExclude = null,
        IReadOnlyList<string>? sourceRoots = null) => new(
        targetAssemblies ?? [], projects ?? [], projectInclude ?? [], projectExclude ?? [], sourceRoots ?? []);

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

    private static ArchitecturePolicyContextContractFact FactItems(
        string name,
        params ArchitecturePolicyContextContractFact[] items) => new(name, [], items);

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
