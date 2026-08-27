using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureChangeSnapshotProjectorTests
{
    private static readonly string[] _violationEvidence = ["Acme.Service.Run: System.Console.WriteLine"];

    [Test]
    public void Project_CanonicalizesProjectIdentityAcrossCheckoutRoots()
    {
        ArchitectureChangeSnapshot linuxSnapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("/home/agent/repository", "/home/agent/repository/src/Acme/Acme.csproj"),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());
        ArchitectureChangeSnapshot windowsSnapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("D:\\a\\repository", "D:\\a\\repository\\src\\Acme\\Acme.csproj"),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());

        ArchitectureChangeReport report = ArchitectureChangeReports.Compare(linuxSnapshot, windowsSnapshot);

        Assert.Multiple(() =>
        {
            Assert.That(linuxSnapshot.Entries.Single().Identity, Is.EqualTo("src/Acme/Acme.csproj"));
            Assert.That(windowsSnapshot.Entries.Single().Identity, Is.EqualTo("src/Acme/Acme.csproj"));
            Assert.That(report.Added, Is.Empty);
            Assert.That(report.Removed, Is.Empty);
        });
    }

    [Test]
    public void Project_MapsGraphSemanticCoverageFindingAndBaselineFacts()
    {
        ArchitectureViolationIdentity identity = Identity(occurrence: 0);
        ArchitectureViolation violation = Violation(identity);
        ArchitectureClassificationRoleFact role = new(
            "Acme.Order",
            "aggregate",
            ArchitectureClassificationSource.Namespace,
            null,
            new Dictionary<string, object>
            {
                ["rank"] = 2.5m,
                ["bounded_context"] = "Sales",
            });
        ArchitectureCoverageSummary coverage = new(
            "coverage",
            "coverage-id",
            "namespace",
            new ArchitectureCoverageSummaryCounts(0, 0, 1, 1, 1),
            Array.Empty<ArchitectureCoverageSummaryExcludedItem>(),
            new[] { new ArchitectureCoverageSummaryEvidenceItem("Acme.Uncovered", "evidence") },
            new[] { new ArchitectureCoverageSummaryEvidenceItem("Acme.Stale", "evidence") },
            new[] { new ArchitectureCoverageSummaryEvidenceItem("Acme.Unknown", "evidence") },
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>());
        ArchitectureGraphOutcome namespaceGraph = Graph(
            new ArchitectureGraphNode("Acme", ArchitectureGraphNodeKind.Namespace),
            new ArchitectureGraphEdge("Acme.Api", "Acme.Domain", ArchitectureGraphNodeKind.Namespace, ArchitectureGraphNodeKind.Namespace, Array.Empty<string>()));
        ArchitectureGraphOutcome assemblyGraph = Graph(
            new ArchitectureGraphNode("Acme", ArchitectureGraphNodeKind.Assembly),
            new ArchitectureGraphEdge("Acme.Api", "Acme.Domain", ArchitectureGraphNodeKind.Assembly, ArchitectureGraphNodeKind.Assembly, Array.Empty<string>()));
        string canonicalIdentity = ArchitectureFindingMapper.FromViolations(new[] { violation }, "strict").Single().CanonicalIdentity;

        ArchitectureChangeSnapshot snapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("/repo", "/repo/src/Acme/Acme.csproj", new[] { violation }, new[] { coverage }, new[] { role }),
            namespaceGraph,
            assemblyGraph,
            new[] { new ArchitectureBaselineComparisonEntry("group", "id", "Acme.Order", "forbidden", null, identity) },
            "ci");

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.ConditionSetName, Is.EqualTo("ci"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("Acme"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("src/Acme/Acme.csproj"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("namespace:Acme.Api->Acme.Domain"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("assembly:Acme.Api->Acme.Domain"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("Acme.Order|aggregate|bounded_context=Sales;rank=2.5"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("Acme.Order|bounded_context|Sales"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("coverage-id|namespace|uncovered|Acme.Uncovered"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("coverage-id|namespace|stale|Acme.Stale|evidence"));
            Assert.That(snapshot.Entries.Select(static entry => entry.Identity), Does.Contain("coverage-id|namespace|unknown|Acme.Unknown|evidence"));
            Assert.That(snapshot.Findings.Single().Identity, Is.EqualTo(canonicalIdentity));
            Assert.That(snapshot.BaselineDebt, Is.EqualTo(new[] { canonicalIdentity }));
        });
    }

    [Test]
    public void Project_ExpandsAggregatedViolationIdentitiesBeforeComparingDrift()
    {
        ArchitectureViolationIdentity knownIdentity = Identity(occurrence: 0);
        ArchitectureViolationIdentity newIdentity = Identity(occurrence: 1);
        ArchitectureViolation aggregate = Violation(knownIdentity) with { Identities = new[] { knownIdentity, newIdentity } };
        string knownCanonicalIdentity = ArchitectureFindingMapper
            .FromViolations(new[] { aggregate })
            .Single(finding => finding.Identity?.Occurrence == knownIdentity.Occurrence)
            .CanonicalIdentity;
        string newCanonicalIdentity = ArchitectureFindingMapper
            .FromViolations(new[] { aggregate })
            .Single(finding => finding.Identity?.Occurrence == newIdentity.Occurrence)
            .CanonicalIdentity;
        ArchitectureChangeSnapshot current = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("/repo", "/repo/src/Acme/Acme.csproj", new[] { aggregate }),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());
        ArchitectureChangeSnapshot baseline = new(
            ArchitectureChangeSnapshot.CurrentSchemaVersion,
            "strict",
            string.Empty,
            Array.Empty<ArchitectureChangeEntry>(),
            new[] { current.Findings.Single(finding => finding.Identity == knownCanonicalIdentity) },
            Array.Empty<string>());

        ArchitectureChangeReport report = ArchitectureChangeReports.Compare(baseline, current);

        Assert.Multiple(() =>
        {
            Assert.That(current.Findings, Has.Count.EqualTo(2));
            Assert.That(report.ExistingFindings, Has.Count.EqualTo(1));
            Assert.That(report.NewFindings, Has.Count.EqualTo(1));
            Assert.That(report.NewFindings[0].Identity, Is.EqualTo(newCanonicalIdentity));
        });
    }

    [Test]
    public void Project_DistinguishesPolicyConsistencyOccurrencesOfTheSameCheckKind()
    {
        // Regression for #683: two "unmatched-layer-exclusion" findings share ContractName
        // ("<policy-consistency>"), ContractId (null), CheckKind, and no RepresentativeType — only
        // Layers (and, when they also collide, PolicyLocation) tell them apart. Before the fix both
        // collapsed to the same canonical identity and SerializeSnapshot rejected the snapshot as
        // "duplicate or empty finding identities".
        PolicyConsistencyDiagnostic first = UnmatchedLayerExclusion("contracts", "Acme.Legacy", "exclude[0]");
        PolicyConsistencyDiagnostic second = UnmatchedLayerExclusion("services", "Acme.Old", "exclude[0]");
        PolicyConsistencyDiagnostic sameLayerDifferentExclusion = UnmatchedLayerExclusion("contracts", "Acme.Deprecated", "exclude[1]");

        ArchitectureChangeSnapshot snapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome(
                "/repo",
                "/repo/src/Acme/Acme.csproj",
                policyConsistencyFindings: new[] { first, second, sameLayerDifferentExclusion }),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Findings, Has.Count.EqualTo(3));
            Assert.That(snapshot.Findings.Select(static finding => finding.Identity).Distinct().Count(), Is.EqualTo(3));
            Assert.DoesNotThrow(() => ArchitectureChangeReports.SerializeSnapshot(snapshot));
        });
    }

    [Test]
    public void Project_IncludesUnmatchedIgnoredViolationsAsFindings()
    {
        ArchitectureUnmatchedIgnoredViolation unmatched = new(
            "contracts-no-forbidden", "contracts-no-forbidden", 0, "Acme.Service", "System.Console", "unused ignore");

        ArchitectureChangeSnapshot snapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome(
                "/repo",
                "/repo/src/Acme/Acme.csproj",
                unmatchedIgnoredViolations: new[] { unmatched }),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Findings.Single().Kind, Is.EqualTo("unmatched_ignore"));
            Assert.DoesNotThrow(() => ArchitectureChangeReports.SerializeSnapshot(snapshot));
        });
    }

    [Test]
    public void Project_ExcludesPolicyLevelFindingsWhenTheirContractFamilyIsOff()
    {
        ArchitectureChangeSnapshot snapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome(
                "/repo",
                "/repo/src/Acme/Acme.csproj",
                policyConsistencyFindings: new[] { UnmatchedLayerExclusion("contracts", "Acme.Legacy", "exclude[0]") },
                policyConsistencyConfig: "off",
                unmatchedIgnoredViolations: new[]
                {
                    new ArchitectureUnmatchedIgnoredViolation(
                        "contracts-no-forbidden", "contracts-no-forbidden", 0, "Acme.Service", "System.Console", "unused ignore"),
                },
                unmatchedIgnoredViolationsConfig: "off"),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());

        Assert.That(snapshot.Findings, Is.Empty);
    }

    // Mirrors ArchitecturePolicyConsistencyAnalysisService.CreateUnmatchedExclusionFinding: real
    // findings of this check kind set RepresentativeType to "<layer>|<namespace pattern>" (#683 PR
    // review, P2) — PolicyLocation/yamlPath is passed through for realism but is no longer what
    // makes the identity unique.
    private static PolicyConsistencyDiagnostic UnmatchedLayerExclusion(
        string layerName, string namespacePattern, string yamlPath) => new(
        "<policy-consistency>",
        null,
        "unmatched-layer-exclusion",
        $"Layer '{layerName}' declares an exclude entry which matches no namespace within the layer's included scope.",
        Array.Empty<string>(),
        Array.Empty<string>(),
        new[] { layerName })
        {
            PolicyLocation = new ArchitecturePolicySourceLocation(
            new ArchitecturePolicySourceDescriptor(
                "/repo", "/repo/architecture/dependencies.arch.yml", ArchitecturePolicyDocumentRole.Root,
                0, null, null, Array.Empty<string>()),
            $"layers.{layerName}.{yamlPath}",
            1,
            1,
            null,
            null),
            RepresentativeType = layerName + "|" + namespacePattern,
        };

    [Test]
    public void Project_DistinguishesCoverageBlindSpotEntriesForDifferentRuleInputsOnSameContract()
    {
        // Regression for #683's actual root cause: BuildRuleInputSummary keys stale/unknown Item on
        // the referenced contract id alone, so a contract with two problematic rule inputs (e.g. its
        // "source" layer and one of its "forbidden" layers) reported two summary items sharing the
        // same Item. The entry identity here only used Item, so both collapsed to one
        // coverage_blind_spot entry and ArchitectureChangeReports.Validate rejected the snapshot with
        // "duplicate or empty entry identities" — the exact wording reported in #683.
        ArchitectureCoverageSummary summary = new(
            "rule-input-coverage",
            "rule-input-coverage",
            "rule_input",
            new ArchitectureCoverageSummaryCounts(0, 0, 0, 2, 0),
            Array.Empty<ArchitectureCoverageSummaryExcludedItem>(),
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            new[]
            {
                new ArchitectureCoverageSummaryEvidenceItem("ghost-rule", "source:ghost"),
                new ArchitectureCoverageSummaryEvidenceItem("ghost-rule", "forbidden:ghost"),
            },
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
            Array.Empty<ArchitectureCoverageSummaryEvidenceItem>());

        ArchitectureChangeSnapshot snapshot = ArchitectureChangeSnapshotProjector.Project(
            "strict",
            Outcome("/repo", "/repo/src/Acme/Acme.csproj", coverageSummaries: new[] { summary }),
            EmptyGraph(),
            EmptyGraph(),
            Array.Empty<ArchitectureBaselineComparisonEntry>());

        ArchitectureChangeEntry[] blindSpots = snapshot.Entries
            .Where(static entry => entry.Kind == "coverage_blind_spot")
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(blindSpots, Has.Length.EqualTo(2));
            Assert.That(blindSpots.Select(static entry => entry.Identity).Distinct().Count(), Is.EqualTo(2));
            Assert.DoesNotThrow(() => ArchitectureChangeReports.SerializeSnapshot(snapshot));
        });
    }

    private static ArchitectureGraphOutcome EmptyGraph() => new(new ArchitectureDependencyGraph(
        Array.Empty<ArchitectureGraphNode>(), Array.Empty<ArchitectureGraphEdge>()));

    private static ArchitectureGraphOutcome Graph(ArchitectureGraphNode node, ArchitectureGraphEdge edge) => new(
        new ArchitectureDependencyGraph(new[] { node }, new[] { edge }));

    private static ArchitectureViolationIdentity Identity(int occurrence) => new(
        ArchitectureViolationIdentity.CurrentVersion,
        "method_body",
        "call",
        "forbidden-call",
        "Acme",
        "Acme.Service",
        "Run",
        "System.Console",
        "System.Console",
        "WriteLine",
        occurrence);

    private static ArchitectureViolation Violation(ArchitectureViolationIdentity identity) => new(
        "forbidden-call",
        "forbidden-call",
        "Acme.Service",
        "System.Console",
        _violationEvidence)
    {
        Identity = identity,
    };

    private static ValidationOutcome Outcome(
        string repositoryRoot,
        string projectPath,
        IReadOnlyCollection<ArchitectureViolation>? violations = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationRoleFact>? roles = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        string policyConsistencyConfig = "error",
        IReadOnlyList<ArchitectureUnmatchedIgnoredViolation>? unmatchedIgnoredViolations = null,
        string unmatchedIgnoredViolationsConfig = "error") => new(
        Passed: true,
        Violations: violations ?? Array.Empty<ArchitectureViolation>(),
        Cycles: Array.Empty<string>(),
        CoverageFindings: Array.Empty<ArchitectureViolation>(),
        CoverageConfig: "off",
        UnmatchedIgnoredViolations: unmatchedIgnoredViolations ?? Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
        UnmatchedIgnoredViolationsConfig: unmatchedIgnoredViolations is null ? "off" : unmatchedIgnoredViolationsConfig,
        PolicyConsistencyFindings: policyConsistencyFindings ?? Array.Empty<PolicyConsistencyDiagnostic>(),
        PolicyConsistencyConfig: policyConsistencyFindings is null ? "off" : policyConsistencyConfig,
        CoverageSummaries: coverageSummaries ?? Array.Empty<ArchitectureCoverageSummary>(),
        ClassificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
        ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            RepositoryRoot = repositoryRoot,
            DiscoveredProjectPaths = new[] { projectPath },
            ClassificationRoles = roles ?? Array.Empty<ArchitectureClassificationRoleFact>(),
        };
}
