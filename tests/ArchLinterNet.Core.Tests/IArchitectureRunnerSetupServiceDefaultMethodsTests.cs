using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// IArchitectureRunnerSetupService carries three C# default interface method (DIM) bodies: the
// 4-arg LoadDocument overload forwards to the 3-arg one while observing cancellation both before
// and after the call, and PrepareRunner/MaterializePreparedRunner default to throwing
// NotSupportedException for any implementation that does not override metadata-only preparation.
// ArchitectureRunnerSetupService (the only production implementation) overrides all three, so
// these default bodies are otherwise unreachable in this codebase. A DIM body only executes when
// invoked through an interface-typed reference on an implementation that does NOT override it —
// calling through a concrete class reference never reaches it even if the class happens to skip
// the override, so this fixture's fake deliberately implements only the members the interface
// requires and is always invoked via its IArchitectureRunnerSetupService-typed field.
[TestFixture]
public sealed class IArchitectureRunnerSetupServiceDefaultMethodsTests
{
    private sealed class MinimalRunnerSetupService : IArchitectureRunnerSetupService
    {
        public int ThreeArgLoadDocumentCallCount { get; private set; }

        public Action? OnThreeArgLoadDocument { get; set; }

        public ArchitectureContractDocument DocumentToReturn { get; set; } =
            new() { Version = 1, Name = "from-three-arg-overload" };

        public ArchitectureContractDocument LoadDocument(
            string policyPath, string? baselinePath = null, ValidationTiming? timing = null)
        {
            ThreeArgLoadDocumentCallCount++;
            OnThreeArgLoadDocument?.Invoke();
            return DocumentToReturn;
        }

        public ArchitectureRunnerSetup BuildRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null)
        {
            throw new NotImplementedException("Not exercised by these DIM-focused tests.");
        }

        public ArchitectureRunnerSetup BuildRunnerForPostBuild(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null)
        {
            throw new NotImplementedException("Not exercised by these DIM-focused tests.");
        }

        // PrepareRunner and MaterializePreparedRunner are deliberately NOT overridden here — the
        // whole point of this fake is to let the interface's own default bodies run.
    }

    [Test]
    public void DefaultLoadDocumentOverload_AlreadyCancelledToken_ThrowsBeforeForwardingToThreeArgOverload()
    {
        var fake = new MinimalRunnerSetupService();
        IArchitectureRunnerSetupService service = fake;
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => service.LoadDocument("policy.arch.yml", null, null, cts.Token));

        // The pre-call ThrowIfCancellationRequested must fire before the 3-arg overload is ever
        // reached — proving the "before" half of the default body's cancellation observation.
        Assert.That(fake.ThreeArgLoadDocumentCallCount, Is.Zero);
    }

    [Test]
    public void DefaultLoadDocumentOverload_CancelledDuringThreeArgOverload_ThrowsAfterForwarding()
    {
        var fake = new MinimalRunnerSetupService();
        IArchitectureRunnerSetupService service = fake;
        using CancellationTokenSource cts = new();
        // The token starts live; the 3-arg overload itself cancels it as a side effect, proving
        // the default body's *second* ThrowIfCancellationRequested (after delegating) is what
        // actually observes and raises the cancellation, not merely a pre-check.
        fake.OnThreeArgLoadDocument = () => cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => service.LoadDocument("policy.arch.yml", null, null, cts.Token));

        Assert.That(fake.ThreeArgLoadDocumentCallCount, Is.EqualTo(1));
    }

    [Test]
    public void DefaultLoadDocumentOverload_NotCancelled_ForwardsToThreeArgOverloadAndReturnsItsDocument()
    {
        var fake = new MinimalRunnerSetupService();
        IArchitectureRunnerSetupService service = fake;

        ArchitectureContractDocument document =
            service.LoadDocument("policy.arch.yml", "baseline.json", timing: null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(fake.ThreeArgLoadDocumentCallCount, Is.EqualTo(1));
            Assert.That(document, Is.SameAs(fake.DocumentToReturn));
        });
    }

    [Test]
    public void DefaultPrepareRunner_ThrowsNotSupportedException()
    {
        IArchitectureRunnerSetupService service = new MinimalRunnerSetupService();
        var document = new ArchitectureContractDocument { Version = 1, Name = "Fake" };

        NotSupportedException? exception = Assert.Throws<NotSupportedException>(
            () => service.PrepareRunner(document, "policy.arch.yml"));

        Assert.That(exception!.Message, Does.Contain("Metadata-only runner preparation"));
    }

    [Test]
    public void DefaultMaterializePreparedRunner_ThrowsNotSupportedException()
    {
        IArchitectureRunnerSetupService service = new MinimalRunnerSetupService();
        var document = new ArchitectureContractDocument { Version = 1, Name = "Fake" };
        var preparation = new ArchitectureRunnerPreparation(
            "/fake/repo", null, ProjectDiscoveryResult.Empty, ResolveAssemblyOutputs: false,
            Array.Empty<string>(), new Dictionary<string, string>(), Array.Empty<string>(),
            IsMetadataReferenceClosureComplete: true);

        NotSupportedException? exception = Assert.Throws<NotSupportedException>(
            () => service.MaterializePreparedRunner(document, preparation));

        Assert.That(exception!.Message, Does.Contain("Prepared runner materialization"));
    }
}
