using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Regresses a bug in PublicApiSurfaceChecker.DeclaredEntries: an unattributed inline `declared_api`
// entry was being lifted to ONE specific live assembly's exact signature (via Dictionary.TryAdd,
// picking whichever assembly the scan happened to enumerate first), turning a wildcard declaration
// into an assembly-pinned one. When two assemblies legitimately export the same base signature —
// duplicate exports across friend/multi-targeted assemblies are not unusual — the other assembly's
// export would then be reported as an undeclared addition, even though the policy never restricted
// the inline entry to any particular assembly.
[TestFixture]
public sealed class PublicApiSurfaceCheckerDeclaredEntriesTests
{
    private const string BaseSignature = "class Acme.Shared.Thing";

    private static ArchitecturePublicApiSurfaceContract Contract(params string[] assemblies)
    {
        return new ArchitecturePublicApiSurfaceContract
        {
            Name = "surface",
            Assemblies = assemblies.ToList(),
            DeclaredApi = new List<string> { BaseSignature },
        };
    }

    private static ArchitectureExportedApiEntry Entry(string assemblyName, string exactSignature)
    {
        return new ArchitectureExportedApiEntry(
            BaseSignature, exactSignature, "Acme.Shared.Thing", assemblyName, "public", false, null,
            Array.Empty<(string, string)>());
    }

    [Test]
    public void DeclaredEntries_IdenticalDuplicateExport_LiftsOneWildcardMatchingBothAssemblies()
    {
        Dictionary<string, List<ArchitectureExportedApiEntry>> scanned = new()
        {
            ["Acme.One"] = new() { Entry("Acme.One", BaseSignature) },
            ["Acme.Two"] = new() { Entry("Acme.Two", BaseSignature) },
        };

        List<PublicApiSnapshotEntry> declared = PublicApiSurfaceChecker.DeclaredEntries(
            Contract("Acme.One", "Acme.Two"), scanned, exactGrammar: true);

        Assert.Multiple(() =>
        {
            Assert.That(declared.Select(entry => entry.AssemblyName), Is.All.EqualTo(PublicApiSnapshotDiffer.WildcardAssembly));
            Assert.That(declared.Select(entry => entry.Signature), Is.EqualTo(new[] { BaseSignature }));
        });
    }

    [Test]
    public void DeclaredEntries_DuplicateExportWithDifferentExactVariants_PreservesWildcardForEachVariant()
    {
        Dictionary<string, List<ArchitectureExportedApiEntry>> scanned = new()
        {
            ["Acme.One"] = new() { Entry("Acme.One", $"{BaseSignature} [sealed]") },
            ["Acme.Two"] = new() { Entry("Acme.Two", BaseSignature) },
        };

        List<PublicApiSnapshotEntry> declared = PublicApiSurfaceChecker.DeclaredEntries(
            Contract("Acme.One", "Acme.Two"), scanned, exactGrammar: true);

        Assert.Multiple(() =>
        {
            // Neither lifted entry is pinned to a specific assembly: the inline declaration never
            // named one, so both of Acme.One's and Acme.Two's exports must still be able to match.
            Assert.That(declared.Select(entry => entry.AssemblyName), Is.All.EqualTo(PublicApiSnapshotDiffer.WildcardAssembly));
            Assert.That(
                declared.Select(entry => entry.Signature),
                Is.EquivalentTo(new[] { $"{BaseSignature} [sealed]", BaseSignature }));
        });
    }

    [Test]
    public void DeclaredEntries_ThroughDiffer_DuplicateExportDoesNotReportEitherAssemblyAsAdded()
    {
        Dictionary<string, List<ArchitectureExportedApiEntry>> scanned = new()
        {
            ["Acme.One"] = new() { Entry("Acme.One", $"{BaseSignature} [sealed]") },
            ["Acme.Two"] = new() { Entry("Acme.Two", BaseSignature) },
        };

        List<PublicApiSnapshotEntry> declared = PublicApiSurfaceChecker.DeclaredEntries(
            Contract("Acme.One", "Acme.Two"), scanned, exactGrammar: true);
        List<PublicApiSnapshotEntry> actual = scanned.Values
            .SelectMany(entries => entries)
            .Select(entry => new PublicApiSnapshotEntry(entry.AssemblyName, entry.ExactSignature))
            .ToList();

        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(declared, actual);

        Assert.That(delta.HasChanges, Is.False);
    }
}
