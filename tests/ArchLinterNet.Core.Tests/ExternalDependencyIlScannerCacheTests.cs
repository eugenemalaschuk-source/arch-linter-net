using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// ArchitectureExternalDependencyIlScanner caches IL-token resolution per scanner instance and
// external-group matching per call (issue #419). These tests pin that the caches never change what
// the scanner reports: every cached run must equal what independent, uncached runs produce.
[TestFixture]
public sealed class ExternalDependencyIlScannerCacheTests
{
    private static readonly Type[] _mixedSourceTypes =
    {
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithMethodCall),
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithConstructorCall),
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithPropertyAccess),
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithGenericOnlyInBody),
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreGenericTypeWithVendorCall<>),
        typeof(ExternalDependencyContractTestsFixtures.Core.CoreTypeWithGenericMethodVendorCall),
        typeof(ExternalDependencyContractTestsFixtures.Core.PureCoreType),
        typeof(ExternalDependencyContractTestsFixtures.UnityStyle.CoreTypeWithUnityMethodBody)
    };

    [Test]
    public void FindMethodBodyViolations_RepeatedCallsOnOneScanner_MatchFreshScannerResults()
    {
        ArchitectureExternalDependencyGroup group = VendorSdkGroup();
        var scanner = new ArchitectureExternalDependencyIlScanner();

        string[] first = Describe(scanner.FindMethodBodyViolations(
            _mixedSourceTypes, "vendor_sdk", group, NewExecutionContext()));
        string[] second = Describe(scanner.FindMethodBodyViolations(
            _mixedSourceTypes, "vendor_sdk", group, NewExecutionContext()));
        string[] uncached = Describe(new ArchitectureExternalDependencyIlScanner().FindMethodBodyViolations(
            _mixedSourceTypes, "vendor_sdk", group, NewExecutionContext()));

        Assert.That(first, Is.Not.Empty);
        Assert.That(second, Is.EqualTo(first));
        Assert.That(first, Is.EqualTo(uncached));
    }

    // The per-call match cache is keyed by resolved member but scoped to one external group, so a
    // scanner reused across groups must not leak the previous group's verdicts.
    [Test]
    public void FindMethodBodyViolations_ScannerReusedAcrossGroups_MatchesPerGroupFreshScanners()
    {
        var scanner = new ArchitectureExternalDependencyIlScanner();

        string[] vendorFromShared = Describe(scanner.FindMethodBodyViolations(
            _mixedSourceTypes, "vendor_sdk", VendorSdkGroup(), NewExecutionContext()));
        string[] unityFromShared = Describe(scanner.FindMethodBodyViolations(
            _mixedSourceTypes, "unity_runtime", UnityGroup(), NewExecutionContext()));
        string[] vendorAgainFromShared = Describe(scanner.FindMethodBodyViolations(
            _mixedSourceTypes, "vendor_sdk", VendorSdkGroup(), NewExecutionContext()));

        string[] vendorFresh = Describe(new ArchitectureExternalDependencyIlScanner().FindMethodBodyViolations(
            _mixedSourceTypes, "vendor_sdk", VendorSdkGroup(), NewExecutionContext()));
        string[] unityFresh = Describe(new ArchitectureExternalDependencyIlScanner().FindMethodBodyViolations(
            _mixedSourceTypes, "unity_runtime", UnityGroup(), NewExecutionContext()));

        Assert.That(unityFresh, Is.Not.Empty);
        Assert.That(vendorFromShared, Is.EqualTo(vendorFresh));
        Assert.That(unityFromShared, Is.EqualTo(unityFresh));
        Assert.That(vendorAgainFromShared, Is.EqualTo(vendorFresh));
    }

    // Tokens inside a generic type or generic method resolve against that generic context, so the
    // token cache key must include it: scanning generic and non-generic types together on one
    // scanner must report exactly what scanning each type on its own reports.
    [Test]
    public void FindMethodBodyViolations_GenericAndNonGenericTypesOnOneScanner_MatchPerTypeScans()
    {
        ArchitectureExternalDependencyGroup group = VendorSdkGroup();
        var scanner = new ArchitectureExternalDependencyIlScanner();

        string[] together = Describe(scanner.FindMethodBodyViolations(
            _mixedSourceTypes, "vendor_sdk", group, NewExecutionContext()));

        var separately = new List<string>();
        foreach (Type sourceType in _mixedSourceTypes)
        {
            separately.AddRange(Describe(new ArchitectureExternalDependencyIlScanner().FindMethodBodyViolations(
                new[] { sourceType }, "vendor_sdk", group, NewExecutionContext())));
        }

        Assert.That(together, Is.EqualTo(separately.ToArray()));
        Assert.That(together.Any(entry => entry.Contains("CoreGenericTypeWithVendorCall")), Is.True);
        Assert.That(together.Any(entry => entry.Contains("CoreTypeWithGenericMethodVendorCall")), Is.True);
    }

    // Caching must not move the cancellation boundary: it is still checked once per source type,
    // so a token cancelled after the first type stops the scan at the next type.
    [Test]
    public void FindMethodBodyViolations_TokenCancelledMidEnumeration_StopsAtNextType()
    {
        using CancellationTokenSource cts = new();
        var scanner = new ArchitectureExternalDependencyIlScanner();

        using IEnumerator<ArchitectureViolation> enumerator = scanner.FindMethodBodyViolations(
            _mixedSourceTypes, "vendor_sdk", VendorSdkGroup(), NewExecutionContext(), cts.Token).GetEnumerator();

        Assert.That(enumerator.MoveNext(), Is.True);
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => enumerator.MoveNext());
    }

    private static ArchitectureExternalDependencyGroup VendorSdkGroup()
    {
        return new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = new List<string> { "ExternalDependencyContractTestsFixtures.VendorSdk" }
        };
    }

    private static ArchitectureExternalDependencyGroup UnityGroup()
    {
        return new ArchitectureExternalDependencyGroup
        {
            NamespacePrefixes = new List<string> { "UnityEngine" }
        };
    }

    private static ArchitectureContractExecutionContext NewExecutionContext()
    {
        return new ArchitectureContractExecutionContext(
            "test-contract", "test-id", Array.Empty<ArchitectureIgnoredViolation>(), false, "audit_external", null);
    }

    private static string[] Describe(IEnumerable<ArchitectureViolation> violations)
    {
        return violations
            .Select(violation =>
                $"{violation.SourceType}|{violation.ForbiddenNamespace}|{string.Join(";", violation.ForbiddenReferences)}")
            .ToArray();
    }
}
