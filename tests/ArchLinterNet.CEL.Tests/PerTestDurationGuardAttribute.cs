using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

// The repo's single-test duration rule is enforced at runtime: every test must complete within
// PerTestDurationGuardAttribute.DefaultLimitMs. A test that legitimately needs more time must opt
// out explicitly with [CancelAfter(...)] on the method or fixture — an exemption is a reviewable
// red flag, not a silent allowance.
[assembly: ArchLinterNet.CEL.Tests.PerTestDurationGuard]

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Assembly-level guard that fails any test exceeding the single-test duration limit unless the
/// test explicitly opts out with <see cref="CancelAfterAttribute"/>. Registered via
/// <c>[assembly: PerTestDurationGuard]</c> in this file.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class PerTestDurationGuardAttribute : Attribute, ITestAction
{
    public const int DefaultLimitMs = 15_000;

    private readonly ConcurrentDictionary<string, long> _startedAtTimestamps = new();

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
            FindExemption(test) is not null);
    }

    internal static void CheckDuration(string testFullName, double elapsedMs, bool hasExplicitExemption)
    {
        if (elapsedMs <= DefaultLimitMs)
        {
            return;
        }

        if (hasExplicitExemption)
        {
            TestContext.WriteLine(
                $"WARNING: test '{testFullName}' took {elapsedMs:F0} ms, exceeding the {DefaultLimitMs} ms default " +
                "single-test limit — exempted via [CancelAfter(...)] (reviewable red flag).");
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
