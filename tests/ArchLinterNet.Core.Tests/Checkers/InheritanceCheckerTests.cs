using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using InheritanceContractTestFixtures.Domain;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests.Checkers;

[TestFixture]
public sealed class InheritanceCheckerTests
{
    private static ArchitectureContractExecutionContext CreateExecutionContext()
    {
        return new ArchitectureContractExecutionContext(
            "contract-name", "contract-id", Array.Empty<ArchitectureIgnoredViolation>(),
            enableUnmatchedIgnoreTracking: false, contractGroup: null, baselineCandidates: null);
    }

    [Test]
    public void Check_DirectBaseTypeViolation_ReturnsViolation_WithNoSessionInvolved()
    {
        var document = new ArchitectureContractDocument();
        var typeIndex = new ArchitectureTypeIndex(new[] { typeof(DirectViolation).Assembly });

        var contract = new ArchitectureInheritanceContract
        {
            Name = "No Framework Inheritance",
            Id = "no-framework-base",
            SourceNamespaces = new List<string> { "InheritanceContractTestFixtures.Domain" },
            ForbiddenBaseTypes = new List<string> { "InheritanceContractTestFixtures.Framework.FrameworkBase" },
        };

        List<ArchitectureViolation> violations = InheritanceChecker.Check(contract, document, typeIndex, CreateExecutionContext());

        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.DirectViolation"
            && (v.Payload as InheritancePayload)?.ForbiddenBaseType == "InheritanceContractTestFixtures.Framework.FrameworkBase"), Is.True);
        Assert.That(violations.Any(v =>
            v.SourceType == "InheritanceContractTestFixtures.Domain.CleanDomainType"), Is.False);
    }
}
