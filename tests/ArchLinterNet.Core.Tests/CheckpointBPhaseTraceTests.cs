using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CheckpointBPhaseTraceTests
{
    [Test]
    public void FormatCancellation_ReportsCompletedPhasesInOrderAndActivePhase()
    {
        var trace = new CheckpointBPhaseTrace();
        using (CheckpointBPhaseTrace.PhaseScope policyCheck = trace.Start("arch-linter-net policy check"))
        {
            policyCheck.Complete();
        }

        using CheckpointBPhaseTrace.PhaseScope active = trace.Start("arch-linter-net change snapshot --current");

        string diagnostic = trace.FormatCancellation();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic, Does.StartWith("Checkpoint B v0.8 full-cycle command timings before NUnit cancellation:"));
            Assert.That(diagnostic, Does.Contain("completed ["));
            Assert.That(diagnostic, Does.Contain("arch-linter-net policy check"));
            Assert.That(diagnostic, Does.Contain("active ["));
            Assert.That(diagnostic, Does.Contain("arch-linter-net change snapshot --current"));
            Assert.That(diagnostic.IndexOf("policy check", StringComparison.Ordinal),
                Is.LessThan(diagnostic.IndexOf("change snapshot", StringComparison.Ordinal)));
        });
    }

    [Test]
    public void FormatCancellation_RetainsOnlyTheMostRecentBoundedSetOfPhases()
    {
        var trace = new CheckpointBPhaseTrace();
        for (int index = 0; index < 65; index++)
        {
            using CheckpointBPhaseTrace.PhaseScope phase = trace.Start($"phase-{index}");
            phase.Complete();
        }

        string diagnostic = trace.FormatCancellation();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic, Does.Not.Contain("phase-0"));
            Assert.That(diagnostic, Does.Contain("phase-1"));
            Assert.That(diagnostic, Does.Contain("phase-64"));
        });
    }
}
