using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using NUnit.Framework;
using static ArchLinterNet.Core.Tests.ArchitecturePolicyWeakeningComparerTests;

namespace ArchLinterNet.Core.Tests;

public sealed class ArchitecturePolicyWeakeningPublicApiApprovalTests
{
    [TestCase("exact")]
    [TestCase("additions_only")]
    public void Compare_ReviewedPublicApiAddition_WithMatchingLiveEvidence_IsAcceptedAndReported(string comparisonMode)
    {
        (ArchitecturePolicyContextExport baseline, ArchitecturePolicyContextExport current) = Contexts(comparisonMode);
        ArchitecturePublicApiWeakeningApproval approval = Approval(baseline, current, "api", _newApi);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current)
        {
            PublicApiApprovals = [approval],
            PublicApiLiveEvidence = [LiveEvidence(current, "api", _existingApi, _newApi)],
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.ApprovedPublicApiAdditions.Single().ContractId, Is.EqualTo("api"));
            Assert.That(result.ApprovedPublicApiAdditions.Single().Added, Is.EqualTo(approval.Added));
            Assert.That(ArchitecturePolicyWeakeningFormatter.FormatAsJson(result), Does.Contain("approved_public_api_additions"));
            Assert.That(ArchitecturePolicyWeakeningFormatter.FormatAsSarif(result), Does.Contain("ApprovedPublicApiAddition"));
        });
    }

    [Test]
    public void Compare_HandEditedSnapshotWithoutMatchingLiveApi_RemainsBlocking()
    {
        (ArchitecturePolicyContextExport baseline, ArchitecturePolicyContextExport current) = Contexts("additions_only");
        ArchitecturePublicApiWeakeningApproval approval = Approval(baseline, current, "api", _newApi);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current)
        {
            PublicApiApprovals = [approval],
            PublicApiLiveEvidence = [LiveEvidence(current, "api", _existingApi)],
        });

        Assert.That(result.Findings.Select(finding => finding.ControlIdentity), Does.Contain("public_api_surface:api:resolved_snapshot_entries"));
    }

    [Test]
    public void Compare_AccidentalLivePublicApiExportOutsideApprovedDelta_RemainsBlocking()
    {
        (ArchitecturePolicyContextExport baseline, ArchitecturePolicyContextExport current) = Contexts("additions_only");
        ArchitecturePublicApiWeakeningApproval approval = Approval(baseline, current, "api", _newApi);
        PublicApiSnapshotEntry accidentalApi = new("Sample", "class Sample.AccidentalApi");

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current)
        {
            PublicApiApprovals = [approval],
            PublicApiLiveEvidence = [LiveEvidence(current, "api", _existingApi, _newApi, accidentalApi)],
        });

        Assert.That(result.Findings, Is.Not.Empty);
    }

    [Test]
    public void Compare_ApprovalWithWrongContextDigest_RemainsBlocking()
    {
        (ArchitecturePolicyContextExport baseline, ArchitecturePolicyContextExport current) = Contexts("exact");
        ArchitecturePublicApiWeakeningApproval approval = Approval(baseline, current, "api", _newApi) with
        {
            CurrentContextDigest = "wrong",
        };

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current)
        {
            PublicApiApprovals = [approval],
            PublicApiLiveEvidence = [LiveEvidence(current, "api", _existingApi, _newApi)],
        });

        Assert.That(result.Findings, Is.Not.Empty);
    }

    [Test]
    public void Compare_ApprovalForAnotherContract_RemainsBlocking()
    {
        (ArchitecturePolicyContextExport baseline, ArchitecturePolicyContextExport current) = Contexts("exact");
        ArchitecturePublicApiWeakeningApproval approval = Approval(baseline, current, "other-api", _newApi);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current)
        {
            PublicApiApprovals = [approval],
            PublicApiLiveEvidence = [LiveEvidence(current, "other-api", _existingApi, _newApi)],
        });

        Assert.That(result.Findings, Is.Not.Empty);
    }

    [Test]
    public void Compare_ExactRemovalCannotBeApprovedAsAnAddition()
    {
        ArchitecturePolicyContextExport baseline = Context(contracts:
        [
            Contract("strict", "public_api_surface", "api", [Fact("api_comparison", "exact"), Snapshot(_existingApi)]),
        ]);
        ArchitecturePolicyContextExport current = Context(contracts:
        [
            Contract("strict", "public_api_surface", "api", [Fact("api_comparison", "exact"), Snapshot()]),
        ]);
        ArchitecturePublicApiWeakeningApproval approval = Approval(baseline, current, "api", _newApi);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current)
        {
            PublicApiApprovals = [approval],
            PublicApiLiveEvidence = [LiveEvidence(current, "api")],
        });

        Assert.That(result.Findings, Is.Not.Empty);
    }

    [Test]
    public void Compare_ApprovalCannotHideSelectorWidening()
    {
        (ArchitecturePolicyContextExport baseline, ArchitecturePolicyContextExport current) = Contexts(
            "exact",
            FactItems("surface_selector", Fact("role", "Reviewed")),
            FactItems("surface_selector", Fact("role", "Exported")));
        ArchitecturePublicApiWeakeningApproval approval = Approval(baseline, current, "api", _newApi);

        ArchitecturePolicyWeakeningResult result = ArchitecturePolicyWeakeningComparer.Compare(new(baseline, current)
        {
            PublicApiApprovals = [approval],
            PublicApiLiveEvidence = [LiveEvidence(current, "api", _existingApi, _newApi)],
        });

        Assert.That(result.Findings, Is.Not.Empty);
    }

    private static readonly PublicApiSnapshotEntry _existingApi = new("Sample", "class Sample.Api");
    private static readonly PublicApiSnapshotEntry _newApi = new("Sample", "class Sample.NewApi");

    private static (ArchitecturePolicyContextExport Baseline, ArchitecturePolicyContextExport Current) Contexts(
        string comparisonMode,
        ArchitecturePolicyContextContractFact? baseSelector = null,
        ArchitecturePolicyContextContractFact? currentSelector = null)
    {
        ArchitecturePolicyContextContractFact[] baseSelectors = baseSelector is null ? [] : [baseSelector];
        ArchitecturePolicyContextContractFact[] currentSelectors = currentSelector is null ? [] : [currentSelector];
        ArchitecturePolicyContextContractFact[] baseFacts = [
            Fact("api_comparison", comparisonMode), Snapshot(_existingApi), ..baseSelectors,
        ];
        ArchitecturePolicyContextContractFact[] currentFacts = [
            Fact("api_comparison", comparisonMode), Snapshot(_existingApi, _newApi), ..currentSelectors,
        ];
        return (Context(contracts: [Contract("strict", "public_api_surface", "api", baseFacts)]),
            Context(contracts: [Contract("strict", "public_api_surface", "api", currentFacts)]));
    }

    private static ArchitecturePolicyContextContractFact Snapshot(params PublicApiSnapshotEntry[] entries) => FactItems(
        "resolved_snapshot_entries",
        entries.Select(entry => FactItems("entry", Fact("assembly", entry.AssemblyName), Fact("signature", entry.Signature))).ToArray());

    private static ArchitecturePublicApiWeakeningApproval Approval(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current,
        string contractId,
        params PublicApiSnapshotEntry[] added) => new(
        ArchitecturePublicApiWeakeningApproval.CurrentSchemaVersion,
        ArchitecturePublicApiWeakeningApproval.ApprovalKind,
        ArchitecturePolicyWeakeningFormatter.ComputeContextDigest(baseline),
        ArchitecturePolicyWeakeningFormatter.ComputeContextDigest(current),
        contractId,
        added);

    private static ArchitecturePublicApiLiveEvidence LiveEvidence(
        ArchitecturePolicyContextExport current,
        string contractId,
        params PublicApiSnapshotEntry[] entries) => new(
        ArchitecturePublicApiLiveEvidence.CurrentSchemaVersion,
        ArchitecturePublicApiLiveEvidence.EvidenceKind,
        ArchitecturePolicyWeakeningFormatter.ComputeContextDigest(current),
        contractId,
        entries);
}
