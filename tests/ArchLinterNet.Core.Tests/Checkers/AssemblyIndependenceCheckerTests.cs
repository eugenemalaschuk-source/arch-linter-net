using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.Checkers;

[TestFixture]
public sealed class AssemblyIndependenceCheckerTests
{
    private static readonly Assembly _coreAssembly = typeof(ArchitectureContractDocument).Assembly;
    private static readonly Assembly _testingAssembly = typeof(ArchLinterNet.Testing.ArchitectureAssertions).Assembly;

    private static ArchitectureContractExecutionContext CreateExecutionContext(
        IReadOnlyList<ArchitectureIgnoredViolation>? ignoredViolations = null)
    {
        return new ArchitectureContractExecutionContext(
            "contract-name",
            "contract-id",
            ignoredViolations ?? Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: false,
            contractGroup: null,
            baselineCandidates: null);
    }

    [Test]
    public void Check_DirectReference_ReturnsViolation_WithNoSessionOrDocumentInvolved()
    {
        string coreName = _coreAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyIndependenceContract
        {
            Name = "Assembly Independence",
            Id = "no-cross-talk",
            Assemblies = new List<string> { testingName, coreName }
        };

        var checker = new AssemblyIndependenceChecker();
        List<ArchitectureViolation> violations = checker.Check(
            contract, new[] { _testingAssembly, _coreAssembly }, CreateExecutionContext());

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].SourceType, Is.EqualTo(testingName));
        Assert.That(violations[0].ForbiddenNamespace, Is.EqualTo(coreName));
    }

    [Test]
    public void Check_IgnoredPair_SuppressesViolation()
    {
        string coreName = _coreAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyIndependenceContract
        {
            Name = "Assembly Independence",
            Assemblies = new List<string> { testingName, coreName }
        };

        var ignoredViolations = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = testingName, ForbiddenReference = coreName, Reason = "fixture" },
        };

        var checker = new AssemblyIndependenceChecker();
        List<ArchitectureViolation> violations = checker.Check(
            contract, new[] { _testingAssembly, _coreAssembly }, CreateExecutionContext(ignoredViolations));

        Assert.That(violations, Is.Empty);
    }
}
