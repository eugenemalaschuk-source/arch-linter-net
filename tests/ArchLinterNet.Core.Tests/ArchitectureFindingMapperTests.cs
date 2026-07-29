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
            Assert.That(tokens.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(tokens.Length));
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

    [Test]
    public void FromViolations_ProjectsGroupedEvidenceToEachAuthoritativeIdentity()
    {
        ArchitectureFinding[] dependencyFindings = FindingsFor(
            new DependencyPayload(),
            ["Product.TargetA", "Product.TargetB"],
            ["Product.TargetA", "Product.TargetB"]);
        ArchitectureFinding[] packageFindings = FindingsFor(
            new PackageDependencyPayload("forbidden"),
            ["Example.A@1.0.0", "Example.B@2.0.0"],
            ["Example.A", "Example.B"]);
        ArchitectureFinding[] frameworkFindings = FindingsFor(
            new FrameworkReferencePayload("forbidden",
            [
                new FrameworkReferenceEvidence("Framework.A", "net10.0", true, "Product.csproj"),
                new FrameworkReferenceEvidence("Framework.B", "net9.0", false, "Product.csproj"),
            ]),
            ["Framework.A (net10.0)", "Framework.B (net9.0)"],
            ["Framework.A (net10.0)", "Framework.B (net9.0)"]);

        Assert.Multiple(() =>
        {
            AssertIdentityMatchesSingleReference(dependencyFindings);
            AssertIdentityMatchesSingleReference(packageFindings);
            AssertIdentityMatchesSingleReference(frameworkFindings);
            Assert.That(
                frameworkFindings.Select(finding =>
                    ((FrameworkReferenceDiagnostic)finding.Details).Evidence.Single().FrameworkName),
                Is.EqualTo(new[] { "Framework.A", "Framework.B" }));
        });
    }

    [Test]
    public void FromPolicyError_LocationlessMessageWordingDoesNotAffectIdentity()
    {
        var diagnostic = new ArchitecturePolicyDiagnostic(
            ArchitecturePolicyDiagnosticKind.ImportResolution,
            null,
            [],
            ["architecture.yml", "policies/domain.yml"]);

        ArchitectureFinding first = ArchitectureFindingMapper.FromPolicyError(
            "Imported policy file is missing.", diagnostic, "missing-file");
        ArchitectureFinding reworded = ArchitectureFindingMapper.FromPolicyError(
            "Unable to locate the requested policy fragment.", diagnostic, "missing-file");

        Assert.Multiple(() =>
        {
            Assert.That(first.CanonicalIdentity, Is.EqualTo(reworded.CanonicalIdentity));
            Assert.That(
                first.CanonicalIdentity,
                Does.Not.Contain(((ArchitecturePolicyErrorDiagnostic)first.Details).Message));
            Assert.That(first.Identity?.TargetMember, Is.EqualTo(nameof(ArchitecturePolicyDiagnosticKind.ImportResolution)));
            Assert.That(first.Identity?.SourceMember, Is.EqualTo("1:policies/domain.yml"));
        });
    }

    private static ArchitectureFinding[] FindingsFor(
        IArchitectureDiagnosticPayload payload,
        IReadOnlyCollection<string> references,
        IReadOnlyList<string> identityTargets)
    {
        var violation = new ArchitectureViolation(
            "contract", "contract-id", "Product.Source", "forbidden", references)
        {
            Payload = payload,
            Identities = identityTargets.Select(target => Identity(target)).ToArray(),
        };
        return ArchitectureFindingMapper.FromViolations([violation], "strict").ToArray();
    }

    private static ArchitectureViolationIdentity Identity(string targetMember) =>
        new(
            ArchitectureViolationIdentity.CurrentVersion,
            "strict",
            "dependency",
            "contract-id",
            "Product",
            "Product.Source",
            null,
            null,
            null,
            targetMember,
            0);

    private static void AssertIdentityMatchesSingleReference(IReadOnlyCollection<ArchitectureFinding> findings)
    {
        Assert.That(findings, Has.Count.EqualTo(2));
        foreach (ArchitectureFinding finding in findings)
        {
            string reference = ReferencesOf(finding.Details).Single();
            string targetMember = finding.Identity?.TargetMember
                ?? throw new AssertionException("Expected an authoritative target member.");
            Assert.That(
                reference.Equals(targetMember, StringComparison.Ordinal)
                || reference.StartsWith(targetMember + "@", StringComparison.Ordinal),
                Is.True,
                finding.CanonicalIdentity);
        }
    }

    private static IReadOnlyCollection<string> ReferencesOf(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        DependencyDiagnostic dependency => dependency.ForbiddenReferences,
        PackageDependencyDiagnostic package => package.ForbiddenReferences,
        FrameworkReferenceDiagnostic framework => framework.ForbiddenReferences,
        _ => throw new AssertionException($"Unexpected diagnostic type '{diagnostic.GetType().Name}'."),
    };
}
