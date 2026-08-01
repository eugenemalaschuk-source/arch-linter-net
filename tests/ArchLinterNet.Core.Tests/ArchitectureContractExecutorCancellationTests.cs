using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #375: ArchitectureContractExecutor.Execute reads cancellation from the session's shared
// ArchitectureAnalysisContext (see design.md Decision 3 — a property on the context rather than a
// parameter threaded through every scanning method) and must stop dispatching further contract
// families once cancellation is observed. ArchitectureContractCatalog.Build always populates
// FamiliesInOrder with every registered family (even when a family has zero contracts in this
// document — see ArchitectureContractCatalog.Build's unconditional AddGroup call), so an
// already-cancelled token is observed on the very first family iteration regardless of contract
// content.
[TestFixture]
public sealed class ArchitectureContractExecutorCancellationTests
{
    private static ArchitectureContractDocument CreateEmptyDocument()
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                UnmatchedIgnoredViolations = "off",
                PolicyConsistency = "off",
                Coverage = "off",
            },
        };
    }

    [Test]
    public void Execute_CancellationRequested_ThrowsBeforeDispatchingAnyFamily()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root", Array.Empty<System.Reflection.Assembly>(), Array.Empty<string>(), Array.Empty<string>())
        {
            CancellationToken = cts.Token
        };
        var document = CreateEmptyDocument();
        var runner = new ArchitectureContractRunner(context, document);

        Assert.Throws<OperationCanceledException>(() =>
            new ArchitectureContractExecutor().Execute(
                runner.Session, "strict", new ArchitectureContractHandlerRegistry()));
    }

    [Test]
    public void Execute_NotCancelled_CompletesNormallyWithNoViolations()
    {
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root", Array.Empty<System.Reflection.Assembly>(), Array.Empty<string>(), Array.Empty<string>());
        var document = CreateEmptyDocument();
        var runner = new ArchitectureContractRunner(context, document);

        ArchitectureContractExecutionResult result = new ArchitectureContractExecutor().Execute(
            runner.Session, "strict", new ArchitectureContractHandlerRegistry());

        Assert.That(result.Violations, Is.Empty);
    }
}
