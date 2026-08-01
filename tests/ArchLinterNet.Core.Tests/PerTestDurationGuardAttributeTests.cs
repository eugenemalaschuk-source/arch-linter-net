using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed class PerTestDurationGuardAttributeTests
{
    [Test]
    public void CheckDuration_WithinLimit_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => PerTestDurationGuardAttribute.CheckDuration(
            "fast", PerTestDurationGuardAttribute.DefaultLimitMs - 1, hasExplicitExemption: false));
    }

    [Test]
    public void CheckDuration_ExactlyAtLimit_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => PerTestDurationGuardAttribute.CheckDuration(
            "boundary", PerTestDurationGuardAttribute.DefaultLimitMs, hasExplicitExemption: false));
    }

    [Test]
    public void CheckDuration_OverLimitWithoutExemption_Throws()
    {
        var ex = Assert.Throws<AssertionException>(() => PerTestDurationGuardAttribute.CheckDuration(
            "slow", PerTestDurationGuardAttribute.DefaultLimitMs + 1, hasExplicitExemption: false));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("slow").And.Contain(PerTestDurationGuardAttribute.DefaultLimitMs.ToString()));
    }

    [Test]
    public void CheckDuration_OverLimitWithExemption_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => PerTestDurationGuardAttribute.CheckDuration(
            "slow-exempt", PerTestDurationGuardAttribute.DefaultLimitMs * 8, hasExplicitExemption: true));
    }

    [Test]
    [CancelAfter(120_000)]
    public void EndToEnd_ExemptedLongTest_RunsThroughGuardWithoutFailing()
    {
        // Smoke probe for the assembly-level ITestAction wiring: this test exceeds the default
        // limit, so it proves the guard observes per-test duration, and the [CancelAfter]
        // exemption proves the opt-out path (the guard writes a WARNING to test output).
        Thread.Sleep(PerTestDurationGuardAttribute.DefaultLimitMs + 1_000);
    }
}
