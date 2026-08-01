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

    // PR #416 review round 3: FromViolations previously accepted no CancellationToken at all, so
    // mapping (diagnostic/identity construction) for a large violations set ran to completion
    // before any caller-side per-finding check ever got a chance to run. This proves the token is
    // now checked mid-enumeration — not just before/after the whole call — using a collection
    // whose enumerator cancels as a side effect of being asked for its second item, and asserting
    // the third item was never even fetched from the source collection.
    [Test]
    public void FromViolations_CancelledMidEnumeration_StopsBeforeMappingRemainingViolations()
    {
        var violations = new[]
        {
            new ArchitectureViolation("rule-a", null, "pkg-a", "pkg-b", Array.Empty<string>()),
            new ArchitectureViolation("rule-a", null, "pkg-c", "pkg-d", Array.Empty<string>()),
            new ArchitectureViolation("rule-a", null, "pkg-e", "pkg-f", Array.Empty<string>()),
        };
        using CancellationTokenSource cts = new();
        var collection = new CancelOnItemCollection(violations, cts, cancelBeforeIndex: 1);

        Assert.Throws<OperationCanceledException>(() =>
            ArchitectureFindingMapper.FromViolations(collection, mode: null, cts.Token));

        Assert.That(collection.FetchedCount, Is.EqualTo(2),
            "the loop must stop as soon as cancellation is observed for the second item — the third must never be fetched from the source collection");
    }

    private sealed class CancelOnItemCollection : IReadOnlyCollection<ArchitectureViolation>
    {
        private readonly IReadOnlyList<ArchitectureViolation> _items;
        private readonly CancellationTokenSource _cts;
        private readonly int _cancelBeforeIndex;

        public CancelOnItemCollection(IReadOnlyList<ArchitectureViolation> items, CancellationTokenSource cts, int cancelBeforeIndex)
        {
            _items = items;
            _cts = cts;
            _cancelBeforeIndex = cancelBeforeIndex;
        }

        public int FetchedCount { get; private set; }

        public int Count => _items.Count;

        public IEnumerator<ArchitectureViolation> GetEnumerator()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (i == _cancelBeforeIndex)
                {
                    _cts.Cancel();
                }

                FetchedCount++;
                yield return _items[i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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
