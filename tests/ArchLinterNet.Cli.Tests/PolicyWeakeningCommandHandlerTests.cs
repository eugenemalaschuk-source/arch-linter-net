using ArchLinterNet.Cli.Commands.Policy.Application;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class PolicyWeakeningCommandHandlerTests
{
    [Test]
    public void CaptureLiveEvidence_CapturesAndBindsCurrentContextDigest()
    {
        ArchitecturePolicyContextExport context = Context();
        ArchitecturePublicApiWeakeningApproval approval = Approval(context);
        PublicApiSnapshotEntry entry = new("Sample", "class Sample.Api");
        List<PublicApiCaptureRequest> requests = [];

        List<ArchitecturePublicApiLiveEvidence> evidence = PolicyWeakeningCommandHandler.CaptureLiveEvidence(
            request =>
            {
                requests.Add(request);
                return new PublicApiCaptureOutcome(true, Snapshot(entry), 1, null, []);
            },
            "architecture/dependencies.arch.yml",
            context,
            [approval],
            "ci",
            BuildPreparationMode.EnsureBuilt,
            noRestore: true);

        Assert.Multiple(() =>
        {
            Assert.That(requests.Single().PolicyPath, Is.EqualTo("architecture/dependencies.arch.yml"));
            Assert.That(requests.Single().OutputPath, Is.EqualTo("architecture/public-api-approval-evidence.txt"));
            Assert.That(requests.Single().ConditionSetName, Is.EqualTo("ci"));
            Assert.That(requests.Single().PreparationMode, Is.EqualTo(BuildPreparationMode.EnsureBuilt));
            Assert.That(requests.Single().NoRestore, Is.True);
            Assert.That(evidence.Single().ContextDigest, Is.EqualTo(approval.CurrentContextDigest));
            Assert.That(evidence.Single().ContractId, Is.EqualTo("api"));
            Assert.That(evidence.Single().Entries, Is.EqualTo([entry]));
        });
    }

    [Test]
    public void CaptureLiveEvidence_FailedCaptureThrowsWithCaptureError()
    {
        ArchitecturePolicyContextExport context = Context();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            PolicyWeakeningCommandHandler.CaptureLiveEvidence(
                _ => new PublicApiCaptureOutcome(false, null, 0, null, [], "capture failed"),
                "policy.yml",
                context,
                [Approval(context)],
                conditionSetName: null,
                preparationMode: BuildPreparationMode.EnsureBuilt,
                noRestore: false))!;

        Assert.That(exception.Message, Is.EqualTo("capture failed"));
    }

    [Test]
    public void CaptureLiveEvidence_RejectsUnverifiedOrdinaryBuildStateForApprovals()
    {
        ArchitecturePolicyContextExport context = Context();
        int captureCount = 0;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            PolicyWeakeningCommandHandler.CaptureLiveEvidence(
                _ =>
                {
                    captureCount++;
                    return new PublicApiCaptureOutcome(
                        true,
                        Snapshot(new PublicApiSnapshotEntry("Sample", "class Sample.Api")),
                        1,
                        null,
                        []);
                },
                "policy.yml",
                context,
                [Approval(context)],
                conditionSetName: null,
                preparationMode: BuildPreparationMode.Ordinary,
                noRestore: false))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("--ensure-built"));
            Assert.That(captureCount, Is.Zero);
        });
    }

    [Test]
    public void CaptureLiveEvidence_UsesVerifiedCurrentSurfaceForTheExactApprovedExpansion()
    {
        PublicApiSnapshotEntry existing = new("Sample", "class Sample.Api");
        PublicApiSnapshotEntry addition = new("Sample", "class Sample.NewApi");
        ArchitecturePolicyContextExport baseline = ApiContext(existing);
        ArchitecturePolicyContextExport current = ApiContext(existing, addition);
        ArchitecturePublicApiWeakeningApproval approval = new(
            ArchitecturePublicApiWeakeningApproval.CurrentSchemaVersion,
            ArchitecturePublicApiWeakeningApproval.ApprovalKind,
            ArchitecturePolicyWeakeningFormatter.ComputeContextDigest(baseline),
            ArchitecturePolicyWeakeningFormatter.ComputeContextDigest(current),
            "api",
            [addition]);

        ArchitecturePolicyWeakeningResult staleOrdinaryResult = ArchitecturePolicyWeakeningComparer.Compare(
            new ArchitecturePolicyWeakeningRequest(baseline, current)
            {
                PublicApiApprovals = [approval],
                PublicApiLiveEvidence = [new ArchitecturePublicApiLiveEvidence(
                    ArchitecturePublicApiLiveEvidence.CurrentSchemaVersion,
                    ArchitecturePublicApiLiveEvidence.EvidenceKind,
                    approval.CurrentContextDigest,
                    "api",
                    [existing])],
            });

        List<PublicApiCaptureRequest> requests = [];
        List<ArchitecturePublicApiLiveEvidence> verifiedEvidence = PolicyWeakeningCommandHandler.CaptureLiveEvidence(
            request =>
            {
                requests.Add(request);
                return new PublicApiCaptureOutcome(true, Snapshot(existing, addition), 2, null, []);
            },
            "architecture/dependencies.arch.yml",
            current,
            [approval],
            conditionSetName: "ci",
            preparationMode: BuildPreparationMode.EnsureBuilt,
            noRestore: true);
        ArchitecturePolicyWeakeningResult verifiedResult = ArchitecturePolicyWeakeningComparer.Compare(
            new ArchitecturePolicyWeakeningRequest(baseline, current)
            {
                PublicApiApprovals = [approval],
                PublicApiLiveEvidence = verifiedEvidence,
            });

        Assert.Multiple(() =>
        {
            Assert.That(staleOrdinaryResult.Findings.Select(finding => finding.ControlIdentity),
                Does.Contain("public_api_surface:api:resolved_snapshot_entries"));
            Assert.That(requests.Single().PreparationMode, Is.EqualTo(BuildPreparationMode.EnsureBuilt));
            Assert.That(requests.Single().ConditionSetName, Is.EqualTo("ci"));
            Assert.That(requests.Single().NoRestore, Is.True);
            Assert.That(verifiedResult.Findings, Is.Empty);
            Assert.That(verifiedResult.ApprovedPublicApiAdditions.Single().Added, Is.EqualTo([addition]));
        });
    }

    private static ArchitecturePublicApiWeakeningApproval Approval(ArchitecturePolicyContextExport context) => new(
        ArchitecturePublicApiWeakeningApproval.CurrentSchemaVersion,
        ArchitecturePublicApiWeakeningApproval.ApprovalKind,
        "base",
        ArchitecturePolicyWeakeningFormatter.ComputeContextDigest(context),
        "api",
        [new PublicApiSnapshotEntry("Sample", "class Sample.Api")]);

    private static string Snapshot(params PublicApiSnapshotEntry[] entries) => PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
        PublicApiSnapshotFormat.CurrentVersion,
        "surface",
        entries));

    private static ArchitecturePolicyContextExport ApiContext(params PublicApiSnapshotEntry[] entries) => Context(
    [
        new ArchitecturePolicyContextContract(
            "strict", "public_api_surface", "api", "api", null, "Approved API expansion", [],
            [
                new ArchitecturePolicyContextContractFact("api_comparison", ["exact"], []),
                new ArchitecturePolicyContextContractFact("resolved_snapshot_entries", [], entries.Select(entry =>
                    new ArchitecturePolicyContextContractFact("entry", [],
                    [
                        new ArchitecturePolicyContextContractFact("assembly", [entry.AssemblyName], []),
                        new ArchitecturePolicyContextContractFact("signature", [entry.Signature], []),
                    ])).ToArray()),
            ],
            [], [], [], [], null),
    ]);

    private static ArchitecturePolicyContextExport Context(
        IReadOnlyList<ArchitecturePolicyContextContract>? contracts = null) => new(
        ArchitecturePolicyContextExport.CurrentSchemaVersion,
        "architecture-policy-context",
        new ArchitecturePolicyContextPolicy("Sample", 1, "policy.yml", false),
        new ArchitecturePolicyContextGuardrails("error"),
        new ArchitecturePolicyContextAnalysis([], [], [], [], []),
        [new ArchitecturePolicyContextSource("policy.yml", "root", 0, null, null, [])],
        [],
        contracts ?? [],
        [],
        [],
        [],
        [],
        [],
        [],
        []);
}
