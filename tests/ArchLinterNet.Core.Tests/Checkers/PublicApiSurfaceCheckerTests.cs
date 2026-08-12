using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.Checkers;

[TestFixture]
public sealed class PublicApiSurfaceCheckerTests
{
    private const string TypeName = "PublicApiSurfaceContractTestFixtures.AccidentalPublicType";

    private static readonly Assembly _fixturesAssembly = typeof(PublicApiSurfaceContractTestFixtures.AccidentalPublicType).Assembly;

    private static ArchitectureContractExecutionContext CreateExecutionContext()
    {
        return new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: false, contractGroup: null, baselineCandidates: null);
    }

    [Test]
    public void Check_UndeclaredType_ReturnsViolation_WithNoSessionOrDocumentInvolved()
    {
        string assemblyName = _fixturesAssembly.GetName().Name!;
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "Public API Surface",
            Id = "no-accidental-public",
            Assemblies = new List<string> { assemblyName },
            DeclaredApi = new List<string>(),
        };

        var resolvedAssemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal)
        {
            [assemblyName] = _fixturesAssembly,
        };

        List<ArchitectureViolation> violations = PublicApiSurfaceChecker.Check(
            contract, resolvedAssemblies, CreateExecutionContext(), surfaceSelectorPredicate: null);

        Assert.That(violations.Any(v => v.SourceType == TypeName && (v.Payload as PublicApiSurfacePayload)?.UndeclaredApiSignature == $"class {TypeName}"), Is.True);
    }

    [Test]
    public void Check_UndeclaredApi_RegistersExactlyOneCanonicalCandidatePerViolation()
    {
        string assemblyName = _fixturesAssembly.GetName().Name!;
        var contract = new ArchitecturePublicApiSurfaceContract
        {
            Name = "Public API Surface",
            Id = "no-accidental-public",
            Assemblies = new List<string> { assemblyName },
            DeclaredApi = new List<string>(),
        };
        var candidates = new List<ArchitectureBaselineCandidate>();
        var context = new ArchitectureContractExecutionContext(
            contract.Name, contract.Id, Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: true, contractGroup: "public_api_surface", baselineCandidates: candidates);

        List<ArchitectureViolation> violations = PublicApiSurfaceChecker.Check(
            contract,
            new Dictionary<string, Assembly>(StringComparer.Ordinal) { [assemblyName] = _fixturesAssembly },
            context,
            surfaceSelectorPredicate: null);

        Assert.That(candidates, Has.Count.EqualTo(violations.Count));
        Assert.That(candidates.Select(candidate => candidate.Identity!.Occurrence), Is.All.EqualTo(0));
    }
}
