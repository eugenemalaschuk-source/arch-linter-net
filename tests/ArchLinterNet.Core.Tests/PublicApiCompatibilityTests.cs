using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PublicApiCompatibilityTests
{
    [Test]
    public void ArchitecturePolicyImportException_TwoArgConstructor_StillSetsCategoryAndMessage()
    {
        var exception = new ArchitecturePolicyImportException(
            ArchitecturePolicyImportErrorCategory.SourceShape,
            "Invalid policy.");

        Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.SourceShape));
        Assert.That(exception.Message, Is.EqualTo("Invalid policy."));
        Assert.That(exception.Diagnostic, Is.Null);
    }

    [Test]
    public void ArchitectureValidationResultParams_OldPositionalConstructorAndDeconstruct_StillCompile()
    {
        ArchitectureValidationResultParams @params = new(
            Passed: true,
            Violations: Array.Empty<ArchitectureViolation>(),
            Cycles: Array.Empty<string>(),
            PolicyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
            PolicyConsistencyConfig: "off",
            CoverageFindings: Array.Empty<ArchitectureViolation>(),
            CoverageConfig: "off",
            UnmatchedIgnoredViolations: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
            UnmatchedIgnoredViolationsConfig: "off",
            CoverageSummaries: Array.Empty<ArchitectureCoverageSummary>(),
            Timing: null)
        {
            CycleFindings = Array.Empty<ArchitectureCycleFinding>()
        };

        (bool passed,
            IReadOnlyCollection<ArchitectureViolation> violations,
            IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings,
            string policyConsistencyConfig,
            IReadOnlyCollection<ArchitectureViolation>? coverageFindings,
            string coverageConfig,
            IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatchedIgnoredViolations,
            string unmatchedIgnoredViolationsConfig,
            IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries,
            ValidationTiming? timing) = @params;

        Assert.That(passed, Is.True);
        Assert.That(violations, Is.Empty);
        Assert.That(cycles, Is.Empty);
        Assert.That(policyConsistencyFindings, Is.Empty);
        Assert.That(policyConsistencyConfig, Is.EqualTo("off"));
        Assert.That(coverageFindings, Is.Empty);
        Assert.That(coverageConfig, Is.EqualTo("off"));
        Assert.That(unmatchedIgnoredViolations, Is.Empty);
        Assert.That(unmatchedIgnoredViolationsConfig, Is.EqualTo("off"));
        Assert.That(coverageSummaries, Is.Empty);
        Assert.That(timing, Is.Null);
        Assert.That(@params.CycleFindings, Is.Empty);
    }
}
