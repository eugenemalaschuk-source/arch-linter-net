using System.Text.Json;
using ArchLinterNet.Core.Change;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class PrReportMarkdownRendererTests
{
    [Test]
    public void CleanProjection_RendersIndependentAcceptanceDimensions()
    {
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(evidence: Evidence()), 20);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Architecture acceptance: **pass**"));
            Assert.That(markdown, Does.Contain("Architecture health: `healthy`"));
            Assert.That(markdown, Does.Contain("Report availability: `complete`"));
            Assert.That(markdown, Does.Contain("Effective policy controls: `2`"));
            Assert.That(markdown, Does.Contain("2/2 evaluable"));
            Assert.That(markdown, Does.Contain("Explicit waiver debt: `0`"));
            Assert.That(markdown, Does.Contain("Existing finding debt: `0`"));
            Assert.That(markdown, Does.Contain("New architecture debt: `0`"));
            Assert.That(markdown, Does.Not.Contain("## Blockers"));
            Assert.That(markdown, Does.Not.Contain("## Non-blocking debt"));
            Assert.That(markdown, Does.Not.Contain("Showing 0 of 0"));
            Assert.That(markdown, Does.Not.Contain("score"));
        });
    }

    [Test]
    public void CleanProjection_RendersExactConciseMarkdownWithoutEmptyDrillDowns()
    {
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(evidence: Evidence()));

        const string Expected = """
            # Architecture PR report

            ## Acceptance
            - Architecture acceptance: **pass** (`gate=pass`)
            - Architecture health: `healthy`
            - Report availability: `complete`
            - Effective policy controls: `2` (strict 1, audit 1, coverage 0)
            - Control applicability/evaluability: `pass` — 2/2 evaluable
            - Configured topology: `not_configured`
            - Explicit waiver debt: `0` total (`0` active, `0` stale, `0` expired)
            - Existing finding debt: `0` baseline entries
            - New architecture debt: `0` new baseline entries
            - Policy weakening: `not_configured`
            - Metrics: `not_configured`
            - Required external evidence: `not_configured`

            ## Completeness and evidence
            - Applicability: `pass` — 2/2 evaluable; 0 unassessable.
            ### Applicability controls (2)
            Showing 2 of 2; omitted 0.
            - `control-1` state=`pass` membership=`required` integrity=valid
            - `control-2` state=`pass` membership=`required` integrity=valid
            - Topology evidence: `not_configured`
            - External evidence: `not_configured`

            ## Canonical navigation

            """;

        Assert.That(markdown, Is.EqualTo(Expected.ReplaceLineEndings(Environment.NewLine)));
    }

    [Test]
    public void ActiveWaiverAndExistingFindingDebt_RemainSeparate()
    {
        ArchitecturePrReportProjection projection = CreateProjection(
            inventory: Inventory(new ArchitecturePolicyInventoryIgnoreDebt(1, 1, 0, 0, 0, 0),
                [Waiver("active", "active")]),
            baseline: [Baseline("matched", "existing")]);

        string markdown = PrReportMarkdownRenderer.Render(projection);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Explicit waiver debt: `1`"));
            Assert.That(markdown, Does.Contain("Existing finding debt: `1` baseline entries"));
            Assert.That(markdown, Does.Contain("Waiver lifecycle detail (1)"));
            Assert.That(markdown, Does.Contain("`active` state=`active`"));
        });
    }

    [Test]
    public void NewPolicyWeakening_IsRenderedBeforeOrdinaryDebt()
    {
        ArchitecturePrReportEvidence evidence = Evidence(
            weakening: new ArchitecturePrReportPolicyWeakening(
                1, "policy-weakening", "policy", 1, "high", true,
                [new ArchitecturePrReportPolicyWeakeningFinding(
                    "weak-1", "broadened_waiver", "control-1", "broadened", "high",
                    ["old"], ["new"], ["subject"], null, null, "review")]),
            baseline: [Baseline("matched", "existing")]);
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(evidence: evidence));

        Assert.That(markdown.IndexOf("policy weakening `weak-1`", StringComparison.Ordinal),
            Is.LessThan(markdown.IndexOf("## Non-blocking debt", StringComparison.Ordinal)));
    }

    [Test]
    public void StrictBlocker_DoesNotPromoteAuditFinding()
    {
        ArchitecturePrReportEvidence evidence = Evidence(receipts:
        [
            Receipt("strict", [Finding("strict-finding", "strict", "error", "strict-code")]),
            Receipt("audit", [Finding("audit-finding", "audit", "warning", "audit-code")]),
        ]);

        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(evidence: evidence));
        int blockersStart = markdown.IndexOf("## Blockers", StringComparison.Ordinal);
        int blockersEnd = markdown.IndexOf("## Completeness and evidence", StringComparison.Ordinal);
        string blockers = markdown[blockersStart..blockersEnd];

        Assert.Multiple(() =>
        {
            Assert.That(blockers, Does.Contain("strict-finding"));
            Assert.That(blockers, Does.Not.Contain("audit-finding"));
        });
    }

    [Test]
    public void HostileArtifactValues_CannotInjectMarkdownStructure()
    {
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(change: Change(
            [new ArchitectureChangeEntry(
                "kind`<!--",
                "identity`<!--",
                "[spoof](https://invalid.example)<!--\u0001")])));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("&#96;"));
            Assert.That(markdown, Does.Not.Contain("<!--"));
            Assert.That(markdown, Does.Not.Contain("[spoof](https://invalid.example)"));
            Assert.That(markdown.Where(character => character is not '\r' and not '\n').Any(char.IsControl), Is.False);
        });
    }

    [Test]
    public void BareGfmAutolinksInPlainTextAreNeutralized()
    {
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(change: Change(
            [new ArchitectureChangeEntry("surface", "identity", "https://invalid.example/path and www.invalid.example")])));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Not.Contain("https://invalid.example/path"));
            Assert.That(markdown, Does.Not.Contain("www.invalid.example"));
            Assert.That(markdown, Does.Contain("https\\://invalid.example/path"));
            Assert.That(markdown, Does.Contain("www&#46;invalid.example"));
        });
    }

    [Test]
    public void GitHubAutolinksAndMentionsInPlainTextAreNeutralized()
    {
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(change: Change(
            [new ArchitectureChangeEntry(
                "surface",
                "identity",
                "contact security@example.com, @user, @org/team, #123, and owner/repository#456")])));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Not.Contain("security@example.com"));
            Assert.That(markdown, Does.Not.Contain("@user"));
            Assert.That(markdown, Does.Not.Contain("@org/team"));
            Assert.That(markdown, Does.Not.Contain("#123"));
            Assert.That(markdown, Does.Not.Contain("owner/repository#456"));
            Assert.That(markdown, Does.Contain("security&#64;example.com"));
            Assert.That(markdown, Does.Contain("&#64;user"));
            Assert.That(markdown, Does.Contain("&#64;org/team"));
            Assert.That(markdown, Does.Contain("&#35;123"));
            Assert.That(markdown, Does.Contain("owner/repository&#35;456"));
        });
    }

    [Test]
    public void BaselineLifecycleIntegrityIsBlockingWhileOnlyMatchedDebtIsNonBlocking()
    {
        ArchitecturePrReportEvidence evidence = Evidence(baseline:
        [
            Baseline("new", "baseline-new"),
            Baseline("matched", "baseline-matched"),
            Baseline("resolved", "baseline-resolved"),
            Baseline("stale", "baseline-stale"),
            Baseline("changed", "baseline-changed"),
            Baseline("ambiguous", "baseline-ambiguous"),
            Baseline("configuration-error", "baseline-configuration"),
        ]);
        evidence = evidence with
        {
            DebtGate = evidence.DebtGate with
            {
                Passed = false,
                PersistentDebt = evidence.DebtGate.PersistentDebt with { InSync = false },
            },
        };

        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(evidence: evidence));
        string blockers = Section(markdown, "## Blockers", "## Non-blocking debt");
        string debt = Section(markdown, "## Non-blocking debt", "## Completeness and evidence");

        const string ExpectedBlockers = """
            ## Blockers
            ### Blocking governance and findings (6)
            Showing 6 of 6; omitted 0.
            - baseline lifecycle `ambiguous`: `baseline-ambiguous` status=`ambiguous` layer namespace → Forbidden.Namespace
            - baseline lifecycle `changed`: `baseline-changed` status=`changed` layer namespace → Forbidden.Namespace
            - baseline lifecycle `configuration-error`: `baseline-configuration` status=`configuration-error` layer namespace → Forbidden.Namespace
            - baseline lifecycle `new`: `baseline-new` status=`new` layer namespace → Forbidden.Namespace
            - baseline lifecycle `resolved`: `baseline-resolved` status=`resolved` layer namespace → Forbidden.Namespace
            - baseline lifecycle `stale`: `baseline-stale` status=`stale` layer namespace → Forbidden.Namespace
            """;
        const string ExpectedDebt = """
            ## Non-blocking debt
            - Explicit waiver debt: 0 total (0 active; 0 stale; 0 expired; 0 metadata-incomplete; 0 invalid)
            ### Existing baseline/finding debt (1)
            Showing 1 of 1; omitted 0.
            - `baseline-matched` status=`matched` layer namespace → Forbidden.Namespace
            """;

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Existing finding debt: `1` baseline entries"));
            Assert.That(markdown, Does.Contain("New architecture debt: `1` new baseline entries"));
            Assert.That(blockers, Is.EqualTo(ExpectedBlockers.ReplaceLineEndings(Environment.NewLine).TrimEnd()));
            Assert.That(debt, Is.EqualTo(ExpectedDebt.ReplaceLineEndings(Environment.NewLine).TrimEnd()));
        });
    }

    [Test]
    public void ResolvedBaselineEntry_IsBlockingGateFailureRatherThanNonBlockingDebt()
    {
        ArchitecturePrReportEvidence evidence = Evidence(baseline: [Baseline("resolved", "baseline-resolved")]);
        evidence = evidence with
        {
            DebtGate = evidence.DebtGate with
            {
                Passed = false,
                PersistentDebt = evidence.DebtGate.PersistentDebt with { InSync = false },
            },
        };

        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(
            evidence: evidence,
            gate: ArchitectureHealthGate.Fail));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Architecture acceptance: **fail** (`gate=fail`)"));
            Assert.That(Section(markdown, "## Blockers", "## Completeness and evidence"), Is.EqualTo(
                """
                ## Blockers
                ### Blocking governance and findings (1)
                Showing 1 of 1; omitted 0.
                - baseline lifecycle `resolved`: `baseline-resolved` status=`resolved` layer namespace → Forbidden.Namespace
                """.ReplaceLineEndings(Environment.NewLine).TrimEnd()));
            Assert.That(markdown, Does.Not.Contain("## Non-blocking debt"));
        });
    }

    [Test]
    public void LifecycleStates_PreserveExpiredAndStaleMetadata()
    {
        ArchitectureWaiverLifecycleRecord expired = Waiver("waiver-expired", "expired") with
        {
            Owner = "team",
            Issue = "#12",
            Expires = new DateOnly(2025, 1, 2),
        };
        ArchitectureWaiverLifecycleRecord stale = Waiver("waiver-stale", "stale");
        ArchitecturePrReportEvidence evidence = Evidence(
            inventory: Inventory(new ArchitecturePolicyInventoryIgnoreDebt(2, 0, 1, 1, 0, 0), [expired, stale]),
            lifecycle: new ArchitectureWaiverLifecycleAssessment("strict", [expired, stale], []));

        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(evidence: evidence));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("state=`expired`"));
            Assert.That(markdown, Does.Contain("state=`stale`"));
            Assert.That(markdown, Does.Contain("expires=2025-01-02"));
            Assert.That(markdown, Does.Contain("2 total"));
        });
    }

    [Test]
    public void IncompleteApplicability_RendersEvaluabilityAndCanonicalReason()
    {
        ArchitecturePrReportApplicability applicability = new(
            "unassessable",
            new ArchitecturePrReportApplicabilitySummary(2, 1, 1),
            [new ArchitecturePrReportApplicabilityReason("missing-receipt", new("family", "control-2", null, "evidence-2"))],
            [
                new ArchitecturePrReportApplicabilityControl("control-1", "required", "pass", true, [], null, null),
                new ArchitecturePrReportApplicabilityControl("control-2", "required", "unassessable", false, [], null, null),
            ]);
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(
            evidence: Evidence(applicability: applicability),
            dimensions: [Dimension("applicability", ArchitectureHealthDimensionState.Unassessable), Dimension("topology", ArchitectureHealthDimensionState.NotConfigured)]));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("1/2 evaluable; 1 unassessable"));
            Assert.That(markdown, Does.Contain("`control-2` state=`unassessable`"));
            Assert.That(markdown, Does.Contain("`missing-receipt`"));
            Assert.That(markdown, Does.Contain("`evidence-2`"));
        });
    }

    [Test]
    public void IncompleteTopology_RendersUnmappedAndAmbiguousSubjects()
    {
        ArchitecturePrReportTopology topology = new(
            "strict", "project", 2,
            new ArchitecturePrReportTopologyCounts(2, 0, 0, 1, 1),
            [
                new ArchitecturePrReportTopologySubject("subject-a", "A", "A", "A", "unmapped", [], null),
                new ArchitecturePrReportTopologySubject("subject-b", "B", "B", "B", "ambiguous", [], null),
            ], [], [], []);
        ArchitecturePrReportApplicability applicability = new(
            "pass", new(1, 1, 0), [],
            [new ArchitecturePrReportApplicabilityControl(
                "topology-control", "required", "pass", true, [], null,
                new ArchitecturePrReportApplicabilityRecord("topology-control", "declared_topology", "pass", [],
                    new(null, "topology-control", null, "topology"), topology, null))]);
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(
            evidence: Evidence(applicability: applicability),
            dimensions: [Dimension("applicability", ArchitectureHealthDimensionState.Pass), Dimension("topology", ArchitectureHealthDimensionState.Degrading)]));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("0 mapped, 1 unmapped, 1 ambiguous"));
            Assert.That(markdown, Does.Contain("disposition=`unmapped`"));
            Assert.That(markdown, Does.Contain("disposition=`ambiguous`"));
        });
    }

    [Test]
    public void WrongRevisionExternalEvidence_RendersStaleCanonicalTrustReceipt()
    {
        ArchitecturePrReportExternalEvidence external = new(
            "strict",
            [new ArchitecturePrReportExternalRequirement("sarif", "sarif", true, "tool", "1", "run", true, true, true, null)],
            [])
        {
            TrustReceipts =
            [
                new ArchitecturePrReportExternalEvidenceTrustReceipt(
                    "sarif",
                    ArchitecturePrReportExternalEvidenceTrustState.Stale,
                    SarifEvidenceTrustStatus.WrongRevision,
                    "wrong_revision",
                    "evidence/previous.sarif",
                    "sha256",
                    "run",
                    0,
                    new ArchitecturePrReportExternalEvidenceContext("repo", "previous", "scope")),
            ],
        };
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(
            evidence: Evidence(external: external),
            dimensions: [Dimension("applicability", ArchitectureHealthDimensionState.Pass), Dimension("external_evidence", ArchitectureHealthDimensionState.Unassessable)]));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Required external evidence: `unassessable`"));
            Assert.That(markdown, Does.Contain("External evidence: `unassessable`"));
            Assert.That(markdown, Does.Contain("logical_evidence=`sarif` state=`stale` trust_status=`wrong_revision`"));
            Assert.That(markdown, Does.Contain("revision=`previous`"));
        });
    }

    [Test]
    public void CurrentZeroResultExternalEvidence_RendersLogicalTrustReceipt()
    {
        ArchitecturePrReportExternalEvidence external = new(
            "strict",
            [new ArchitecturePrReportExternalRequirement("sarif", "sarif", true, "tool", "1", "run", true, true, true, null)],
            [])
        {
            TrustReceipts =
            [
                new ArchitecturePrReportExternalEvidenceTrustReceipt(
                    "sarif",
                    ArchitecturePrReportExternalEvidenceTrustState.Current,
                    SarifEvidenceTrustStatus.Valid,
                    "trusted",
                    "evidence/current.sarif",
                    "sha256",
                    "run",
                    0,
                    new ArchitecturePrReportExternalEvidenceContext("repo", "current", "scope")),
            ],
        };
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(
            evidence: Evidence(external: external),
            dimensions: [Dimension("applicability", ArchitectureHealthDimensionState.Pass), Dimension("external_evidence", ArchitectureHealthDimensionState.Pass)]));

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Required external evidence: `pass`"));
            Assert.That(markdown, Does.Contain("External evidence trust receipts (1)"));
            Assert.That(markdown, Does.Contain("logical_evidence=`sarif` state=`current` trust_status=`valid`"));
            Assert.That(markdown, Does.Contain("results=`0`"));
        });
    }

    [Test]
    public void MissingEvidence_IsUnavailableAndAllChangeSectionsKeepCounts()
    {
        ArchitecturePrReportProjection projection = CreateProjection(evidence: null, change: Change(
            [new("surface", "added", "Added")], [new("surface", "removed", "Removed")],
            [new("finding-new", "new", "New")], [new("finding-existing", "existing", "Existing")],
            [new("finding-resolved", "resolved", "Resolved")], ["baseline"]));
        string markdown = PrReportMarkdownRenderer.Render(projection, 1);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Report availability: `unavailable`"));
            Assert.That(markdown, Does.Contain("Canonical report evidence: `unavailable`"));
            Assert.That(markdown, Does.Contain("Added surfaces (1)"));
            Assert.That(markdown, Does.Contain("Removed surfaces (1)"));
            Assert.That(markdown, Does.Contain("New findings (1)"));
            Assert.That(markdown, Does.Contain("Existing findings (1)"));
            Assert.That(markdown, Does.Contain("Resolved findings (1)"));
        });
    }

    [Test]
    public void BoundedSections_PreserveTotalAndOmittedCount()
    {
        ArchitectureChangeEntry[] added = [
            new("surface", "a", "A"), new("surface", "b", "B"), new("surface", "c", "C")];
        string markdown = PrReportMarkdownRenderer.Render(CreateProjection(change: Change(added)), 1);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("Added surfaces (3)"));
            Assert.That(markdown, Does.Contain("Showing 1 of 3; omitted 2."));
            Assert.That(markdown, Does.Contain("`a`"));
            Assert.That(markdown, Does.Not.Contain("`b`"));
        });
    }

    private static ArchitecturePrReportProjection CreateProjection(
        ArchitecturePrReportEvidence? evidence = null,
        ArchitecturePolicyInventory? inventory = null,
        IReadOnlyList<ArchitecturePrReportBaselineEntry>? baseline = null,
        IReadOnlyList<ArchitectureHealthDimension>? dimensions = null,
        ArchitecturePrReportChange? change = null,
        ArchitectureHealthGate gate = ArchitectureHealthGate.Pass) =>
        new(
            new ArchitecturePrReportHeadline(
                gate,
                ArchitectureHealthState.Healthy,
                evidence is null && inventory is null ? ArchitecturePrReportAvailability.Unavailable : ArchitecturePrReportAvailability.Complete,
                dimensions ?? [Dimension("applicability", ArchitectureHealthDimensionState.Pass), Dimension("topology", ArchitectureHealthDimensionState.NotConfigured), Dimension("metrics", ArchitectureHealthDimensionState.NotConfigured), Dimension("external_evidence", ArchitectureHealthDimensionState.NotConfigured)]),
            evidence ?? (inventory is null ? null : Evidence(inventory: inventory, baseline: baseline)),
            change ?? Change(),
            []);

    private static ArchitecturePrReportEvidence Evidence(
        ArchitecturePolicyInventory? inventory = null,
        ArchitectureWaiverLifecycleAssessment? lifecycle = null,
        ArchitecturePrReportApplicability? applicability = null,
        ArchitecturePrReportExternalEvidence? external = null,
        ArchitecturePrReportPolicyWeakening? weakening = null,
        IReadOnlyList<ArchitecturePrReportBaselineEntry>? baseline = null,
        IReadOnlyList<ArchitecturePrReportValidationReceipt>? receipts = null) =>
        new(ArchitecturePrReportEvidence.CurrentSchemaVersion, ArchitecturePrReportEvidence.EvidenceKind, ArchitectureHealthGate.Pass, ArchitectureHealthState.Healthy,
            receipts ?? [Receipt("strict", [], inventory, lifecycle, applicability, external)],
            new(true, weakening is null, new(true, "strict", true, []), new(true, true, baseline ?? [], []), weakening));

    private static ArchitecturePrReportValidationReceipt Receipt(
        string mode,
        IReadOnlyList<ArchitecturePrReportFinding> findings,
        ArchitecturePolicyInventory? inventory = null,
        ArchitectureWaiverLifecycleAssessment? lifecycle = null,
        ArchitecturePrReportApplicability? applicability = null,
        ArchitecturePrReportExternalEvidence? external = null) =>
        new(
            mode,
            new Dictionary<string, string>(),
            inventory ?? Inventory(),
            lifecycle ?? new(mode, [], []),
            applicability ?? Applicability(),
            external,
            findings,
            new("/repo", [], [], []));

    private static ArchitecturePrReportFinding Finding(string identity, string mode, string severity, string code) =>
        new(3, "diagnostic", identity, mode, severity, code, "contract", "contract", null, null, null, default);

    private static ArchitecturePrReportChange Change(
        IReadOnlyList<ArchitectureChangeEntry>? added = null,
        IReadOnlyList<ArchitectureChangeEntry>? removed = null,
        IReadOnlyList<ArchitectureChangeFinding>? newFindings = null,
        IReadOnlyList<ArchitectureChangeFinding>? existingFindings = null,
        IReadOnlyList<ArchitectureChangeFinding>? resolvedFindings = null,
        IReadOnlyList<string>? baselineDebt = null) =>
        new(
            new ArchitecturePrReportExecutionContext("run", string.Empty),
            "strict",
            added ?? [],
            removed ?? [],
            newFindings ?? [],
            existingFindings ?? [],
            resolvedFindings ?? [],
            baselineDebt ?? []);

    private static ArchitecturePolicyInventory Inventory(
        ArchitecturePolicyInventoryIgnoreDebt? debt = null,
        IReadOnlyList<ArchitectureWaiverLifecycleRecord>? waivers = null) =>
        new(ArchitecturePolicyInventory.CurrentSchemaId, 2, new(1, 1, 0), debt ?? new(0, 0, 0, 0, 0, 0), waivers ?? []);

    private static ArchitecturePrReportApplicability Applicability() =>
        new("pass", new(2, 2, 0), [], [
            new ArchitecturePrReportApplicabilityControl("control-1", "required", "pass", true, [], null, null),
            new ArchitecturePrReportApplicabilityControl("control-2", "required", "pass", true, [], null, null)]);

    private static ArchitectureWaiverLifecycleRecord Waiver(string id, string state) =>
        new(id, state, "Rule", id, "layer", "namespace", "Forbidden.Namespace", null, "Reason", null, null,
            new DateOnly(2024, 1, 1), null, new DateOnly(2026, 1, 1), true);

    private static ArchitecturePrReportBaselineEntry Baseline(string status, string identity) =>
        new(status, status, "layer", identity, "namespace", "Forbidden.Namespace", null, null, null, identity);

    private static ArchitectureHealthDimension Dimension(string name, ArchitectureHealthDimensionState state) =>
        new(name, state, []);

    private static string Section(string markdown, string heading, string nextHeading)
    {
        int start = markdown.IndexOf(heading, StringComparison.Ordinal);
        int end = markdown.IndexOf(nextHeading, start, StringComparison.Ordinal);
        return markdown[start..end].TrimEnd();
    }
}
