using ArchLinterNet.Cli.Commands.Policy.Application;
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
            [approval]);

        Assert.Multiple(() =>
        {
            Assert.That(requests.Single().PolicyPath, Is.EqualTo("architecture/dependencies.arch.yml"));
            Assert.That(requests.Single().OutputPath, Is.EqualTo("architecture/public-api-approval-evidence.txt"));
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
                [Approval(context)]))!;

        Assert.That(exception.Message, Is.EqualTo("capture failed"));
    }

    private static ArchitecturePublicApiWeakeningApproval Approval(ArchitecturePolicyContextExport context) => new(
        ArchitecturePublicApiWeakeningApproval.CurrentSchemaVersion,
        ArchitecturePublicApiWeakeningApproval.ApprovalKind,
        "base",
        ArchitecturePolicyWeakeningFormatter.ComputeContextDigest(context),
        "api",
        [new PublicApiSnapshotEntry("Sample", "class Sample.Api")]);

    private static string Snapshot(PublicApiSnapshotEntry entry) => PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
        PublicApiSnapshotFormat.CurrentVersion,
        "surface",
        [entry]));

    private static ArchitecturePolicyContextExport Context() => new(
        ArchitecturePolicyContextExport.CurrentSchemaVersion,
        "architecture-policy-context",
        new ArchitecturePolicyContextPolicy("Sample", 1, "policy.yml", false),
        new ArchitecturePolicyContextGuardrails("error"),
        new ArchitecturePolicyContextAnalysis([], [], [], [], []),
        [new ArchitecturePolicyContextSource("policy.yml", "root", 0, null, null, [])],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        []);
}
