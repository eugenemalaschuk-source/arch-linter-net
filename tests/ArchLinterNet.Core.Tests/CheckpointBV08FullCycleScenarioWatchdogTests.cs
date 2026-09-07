using System.Reflection;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Pins the acceptance criterion that the seven-minute composed watchdog (#771) stays scoped to
/// <c>PackedCandidate_V08FullCycle</c> alone: every other Checkpoint B scenario must keep relying
/// on the fixture's class-level <c>[CancelAfter]</c> bound rather than acquiring its own override.
/// </summary>
[TestFixture]
public sealed class CheckpointBV08FullCycleScenarioWatchdogTests
{
    [Test]
    public void V08FullCycleWatchdog_IsScopedToItsOwnTestOnly()
    {
        MethodInfo[] checkpointBTests = typeof(CheckpointBReleaseGateTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<TestAttribute>() is not null)
            .ToArray();
        Assert.That(checkpointBTests, Has.Length.GreaterThan(1),
            "Diagnostic: expected multiple Checkpoint B [Test] methods to compare watchdog scope against.");

        MethodInfo v08FullCycle = checkpointBTests.Single(
            method => method.Name == nameof(CheckpointBReleaseGateTests.PackedCandidate_V08FullCycle));
        MethodInfo[] otherTests = checkpointBTests.Where(method => method != v08FullCycle).ToArray();
        Assert.That(otherTests, Is.Not.Empty,
            "Diagnostic: expected sibling Checkpoint B scenarios to assert against.");

        Assert.That(GetCancelAfterTimeoutMs(v08FullCycle), Is.EqualTo(CheckpointBV08FullCycleScenario.WatchdogMs),
            $"{v08FullCycle.Name} must carry the composed v0.8 full-cycle watchdog.");

        int?[] otherWatchdogs = otherTests.Select(GetCancelAfterTimeoutMs).ToArray();
        Assert.That(otherWatchdogs, Has.All.Null,
            "Only PackedCandidate_V08FullCycle may override the class-level Checkpoint B watchdog with its own "
            + "composed bound; every other scenario must keep relying on the fixture's shared [CancelAfter].");
    }

    private static int? GetCancelAfterTimeoutMs(MethodInfo test)
    {
        CancelAfterAttribute? cancelAfter = test.GetCustomAttribute<CancelAfterAttribute>();
        return cancelAfter is null ? null : (int)cancelAfter.Properties.Get("Timeout")!;
    }
}
