using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureFindingMapperTests
{
    [Test]
    public void KindToken_MapsEverySupportedDiagnosticKindToStableDistinctWireValue()
    {
        string[] tokens = Enum.GetValues<ArchitectureDiagnosticKind>()
            .Select(ArchitectureFindingMapper.KindToken)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(tokens, Has.Length.EqualTo(Enum.GetValues<ArchitectureDiagnosticKind>().Length));
            Assert.That(tokens.Distinct(StringComparer.Ordinal), Has.Count.EqualTo(tokens.Length));
            Assert.That(tokens, Does.Contain("package_dependency"));
            Assert.That(tokens, Does.Contain("composition"));
            Assert.That(tokens, Does.Contain("build_state_preflight"));
            Assert.That(tokens, Does.Contain("policy_consistency"));
        });
    }

    [Test]
    public void Order_KeepsSameNamedGlobalProgramsDistinctByAssemblyAndMemberIdentity()
    {
        ArchitectureFinding first = ArchitectureFindingMapper.FromDiagnostic(new CompositionDiagnostic(
            "composition", "composition", "Program", "forbidden", ["call"])
        {
            SourceAssembly = "Host.B",
            SourceMember = "Main",
        });
        ArchitectureFinding second = ArchitectureFindingMapper.FromDiagnostic(new CompositionDiagnostic(
            "composition", "composition", "Program", "forbidden", ["call"])
        {
            SourceAssembly = "Host.A",
            SourceMember = "Configure",
        });

        IReadOnlyList<ArchitectureFinding> ordered = ArchitectureFindingMapper.Order([first, second]);

        Assert.Multiple(() =>
        {
            Assert.That(first.CanonicalIdentity, Is.Not.EqualTo(second.CanonicalIdentity));
            Assert.That(ordered.Select(finding => finding.CanonicalIdentity), Is.EqualTo(new[]
            {
                second.CanonicalIdentity, first.CanonicalIdentity,
            }));
        });
    }
}
