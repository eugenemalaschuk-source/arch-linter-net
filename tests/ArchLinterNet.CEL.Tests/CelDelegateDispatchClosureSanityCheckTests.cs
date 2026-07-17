using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Proves <see cref="CelEvaluateCallGraphNeverReachesCompilePipelineTests.ResolveDelegateInvocationTargets"/>
/// actually finds a non-capturing lambda's compiled target via the assembly-wide
/// <c>ldftn</c>/<c>ldvirtftn</c> signature scan, rather than silently returning nothing for every
/// delegate invocation — the pattern <c>ArchLinterNet.CEL.Evaluation.CelEvaluator</c> itself uses
/// for its <c>Func&lt;int, bool&gt;</c>/<c>Func&lt;bool, bool&gt;</c> comparison/projection
/// delegates (e.g. <c>EvaluateOrdering(binary, static comparison =&gt; comparison &lt; 0)</c>).
/// Without this test, a bug that made the resolution always return an empty list would leave the
/// main call-graph test green for the wrong reason: it would just report every delegate invocation
/// as an unresolved edge and fail loudly (safe, but not what the resolution is meant to avoid for
/// the codebase's actual, verified-safe delegate usages) — or, if the emptiness bug went the other
/// way (matching everything), it could silently under-report by matching an unrelated method
/// instead of the real target family. This test exercises the resolution directly against a known
/// input/output pair to confirm it behaves as documented in either direction.
/// </summary>
[TestFixture]
public sealed class CelDelegateDispatchClosureSanityCheckTests
{
    private static bool IsPositive(int value) => value > 0;

    private static readonly Func<int, bool> _capturedDelegate = IsPositive;

    [Test]
    public void ResolveDelegateInvocationTargets_FindsMethodGroupConversionTarget()
    {
        // Constructing _capturedDelegate above emits ldftn IsPositive + newobj Func<int,bool>::.ctor
        // in this type's static constructor — exactly the pattern CelEvaluator's inline lambda
        // arguments compile to. The invoke method we resolve against is Func<int,bool>.Invoke.
        var invokeMethod = typeof(Func<int, bool>).GetMethod(nameof(Func<int, bool>.Invoke))!;

        var targets = CelEvaluateCallGraphNeverReachesCompilePipelineTests.ResolveDelegateInvocationTargets(
            invokeMethod, typeof(CelDelegateDispatchClosureSanityCheckTests).Assembly);

        var expectedTarget = typeof(CelDelegateDispatchClosureSanityCheckTests)
            .GetMethod(nameof(IsPositive), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        Assert.That(targets, Does.Contain(expectedTarget),
            "Must find IsPositive as a possible target of Func<int,bool>.Invoke, via the ldftn " +
            "emitted when _capturedDelegate was constructed from the IsPositive method group.");
    }

    [Test]
    public void ResolveDelegateInvocationTargets_ReturnsEmptyForAnUnusedSignature()
    {
        // No method anywhere in this test assembly is captured as a delegate matching this
        // (string, string) -> int shape, so the sound-over-approximation index should find nothing
        // — confirming the resolution doesn't just match everything indiscriminately.
        var invokeMethod = typeof(Func<string, string, int>).GetMethod(nameof(Func<string, string, int>.Invoke))!;

        var targets = CelEvaluateCallGraphNeverReachesCompilePipelineTests.ResolveDelegateInvocationTargets(
            invokeMethod, typeof(CelDelegateDispatchClosureSanityCheckTests).Assembly);

        Assert.That(targets, Is.Empty);
    }
}
