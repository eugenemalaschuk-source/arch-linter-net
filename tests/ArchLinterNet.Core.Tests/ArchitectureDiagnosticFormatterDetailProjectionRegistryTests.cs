using System.Reflection;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDiagnosticFormatterDetailProjectionRegistryTests
{
    [Test]
    public void All_CoversExactlyEverySealedDiagnosticSubtype()
    {
        var expectedTypes = typeof(ArchitectureDiagnostic).Assembly.GetTypes()
            .Where(t => t.IsSealed && !t.IsAbstract && typeof(ArchitectureDiagnostic).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var registeredTypes = ArchitectureDiagnosticFormatter.DiagnosticDetailProjectionRegistry.All
            .Select(entry => entry.DiagnosticType)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        Assert.That(expectedTypes, Is.Not.Empty, "Sanity check: reflection must find at least one diagnostic subtype.");
        Assert.That(registeredTypes, Is.EqualTo(expectedTypes),
            "Every sealed ArchitectureDiagnostic subtype must have exactly one registered detail projector.");
    }

    [Test]
    public void All_HasNoDuplicateDiagnosticTypes()
    {
        List<Type> types = ArchitectureDiagnosticFormatter.DiagnosticDetailProjectionRegistry.All
            .Select(entry => entry.DiagnosticType)
            .ToList();

        Assert.That(types.Distinct().ToList(), Has.Count.EqualTo(types.Count));
    }

    [Test]
    public void All_EveryEntryHasANonNullProjector()
    {
        foreach (DiagnosticDetailProjectionEntry entry in ArchitectureDiagnosticFormatter.DiagnosticDetailProjectionRegistry.All)
        {
            Assert.That(entry.Projector, Is.Not.Null, $"Diagnostic type '{entry.DiagnosticType.Name}' must expose a live projector delegate.");
        }
    }

    [Test]
    public void ApplyDiagnosticSpecificCiFields_UnregisteredDiagnosticType_ThrowsInvalidOperationException()
    {
        MethodInfo method = typeof(ArchitectureDiagnosticFormatter).GetMethod(
            "ApplyDiagnosticSpecificCiFields", BindingFlags.NonPublic | BindingFlags.Static)!;

        var unregistered = new UnregisteredDiagnostic("contract", null);

        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() =>
            method.Invoke(null, new object[] { unregistered, new Dictionary<string, object?>() }))!;

        Assert.That(thrown.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(thrown.InnerException!.Message, Does.Contain(nameof(UnregisteredDiagnostic)));
    }

    // Defined in the test assembly, not typeof(ArchitectureDiagnostic).Assembly (Core), so the
    // reflection scan in All_CoversExactlyEverySealedDiagnosticSubtype never sees it. It exists
    // only to prove the registry lookup throws for a diagnostic type it has never registered,
    // without requiring a real unregistered production diagnostic to exist.
    private sealed record UnregisteredDiagnostic(string ContractName, string? ContractId)
        : ArchitectureDiagnostic(ContractName, ContractId)
    {
        public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.Dependency;
    }
}
