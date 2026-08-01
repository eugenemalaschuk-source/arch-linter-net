using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

// The repo's single-test duration rule is enforced at runtime: every test must complete within
// PerTestDurationGuardAttribute.DefaultLimitMs. A test that legitimately needs more time must opt
// out explicitly with [CancelAfter(...)] on the method or fixture — an exemption is a reviewable
// red flag, not a silent allowance. Sequential test processes enforce the rule (enforce: true);
// this process registers the guard with enforce: false because it runs tests under 8-way
// parallelism, where a test's wall-clock time is dominated by CPU contention and a hard per-test
// gate would be non-deterministic — here over-limit tests are demoted to warnings.
[assembly: ArchLinterNet.Cli.Tests.PerTestDurationGuard(enforce: false)]

namespace ArchLinterNet.Cli.Tests;

/// <summary>
/// Assembly-level guard that fails any test exceeding the single-test duration limit unless the
/// test explicitly opts out with <see cref="CancelAfterAttribute"/>. Registered via
/// <c>[assembly: PerTestDurationGuard]</c> in this file. Pass <c>false</c> to the constructor to
/// demote over-limit tests to warnings instead of failures (used in parallel test processes, where
/// wall-clock durations are not deterministic).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class PerTestDurationGuardAttribute : Attribute, ITestAction
{
    public const int DefaultLimitMs = 15_000;

    public PerTestDurationGuardAttribute(bool enforce = true)
    {
        Enforce = enforce;
    }

    private readonly ConcurrentDictionary<string, long> _startedAtTimestamps = new();

    public bool Enforce { get; }

    public ActionTargets Targets => ActionTargets.Test;

    public void BeforeTest(ITest test)
    {
        _startedAtTimestamps[test.FullName] = Stopwatch.GetTimestamp();
    }

    public void AfterTest(ITest test)
    {
        if (!_startedAtTimestamps.TryRemove(test.FullName, out long startTimestamp))
        {
            return;
        }

        CheckDuration(
            test.FullName,
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
            FindExemption(test) is not null,
            Enforce);
    }

    internal static void CheckDuration(string testFullName, double elapsedMs, bool hasExplicitExemption, bool enforce = true)
    {
        if (elapsedMs <= DefaultLimitMs)
        {
            return;
        }

        if (!enforce || hasExplicitExemption)
        {
            TestContext.WriteLine(
                $"WARNING: test '{testFullName}' took {elapsedMs:F0} ms, exceeding the {DefaultLimitMs} ms default " +
                $"single-test limit — {(hasExplicitExemption ? "exempted via [CancelAfter(...)] (reviewable red flag)" : "not enforced (parallel test process)")}.");
            return;
        }

        throw new AssertionException(
            $"Test '{testFullName}' took {elapsedMs:F0} ms, exceeding the {DefaultLimitMs} ms single-test duration limit. " +
            "If this test legitimately needs more time, add an explicit [CancelAfter(...)] attribute (a reviewable red flag); " +
            "otherwise it should be sped up.");
    }

    private static CancelAfterAttribute? FindExemption(ITest test)
    {
        MethodInfo? method = test.Method?.MethodInfo;
        if (method is null)
        {
            return null;
        }

        CancelAfterAttribute? methodLevel = method.GetCustomAttribute<CancelAfterAttribute>(inherit: true);
        return methodLevel ?? method.DeclaringType?.GetCustomAttribute<CancelAfterAttribute>(inherit: true);
    }
}
