using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// ExternalEvidenceRequirements is portable run metadata (like SourceExpansion), never part of the
// persisted AnalysisCacheOutcomeV1 payload itself — see AnalysisCacheOutcomeMapper.FromCacheOutcome.
[TestFixture]
public sealed class AnalysisCacheOutcomeMapperExternalEvidenceTests
{
    [Test]
    public void FromCacheOutcome_WithExternalEvidenceRequirements_EchoesThemOnTheReconstructedOutcome()
    {
        AnalysisCacheOutcomeV1 cached = AnalysisCacheOutcomeMapper.ToCacheOutcome(PassingOutcome());
        ArchitectureExternalEvidenceRequirement[] requirements =
        [
            new() { Id = "external.scan", Format = "sarif", Required = true, Tool = "Scanner", Run = "run" },
        ];

        ValidationOutcome reconstructed = AnalysisCacheOutcomeMapper.FromCacheOutcome(
            cached, "/repo", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            ArchitectureSourceExpansionInventory.Empty, mode: "strict",
            externalEvidenceRequirements: requirements);

        Assert.That(reconstructed.ExternalEvidenceRequirements.Single().Id, Is.EqualTo("external.scan"));
    }

    [Test]
    public void FromCacheOutcome_WithoutExternalEvidenceRequirements_DefaultsToEmpty()
    {
        AnalysisCacheOutcomeV1 cached = AnalysisCacheOutcomeMapper.ToCacheOutcome(PassingOutcome());

        ValidationOutcome reconstructed = AnalysisCacheOutcomeMapper.FromCacheOutcome(
            cached, "/repo", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            ArchitectureSourceExpansionInventory.Empty);

        Assert.That(reconstructed.ExternalEvidenceRequirements, Is.Empty);
    }

    private static ValidationOutcome PassingOutcome() => new(
        Passed: true,
        Violations: Array.Empty<ArchitectureViolation>(),
        Cycles: Array.Empty<string>(),
        CoverageFindings: Array.Empty<ArchitectureViolation>(),
        CoverageConfig: "off",
        UnmatchedIgnoredViolations: Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
        UnmatchedIgnoredViolationsConfig: "off",
        PolicyConsistencyFindings: Array.Empty<PolicyConsistencyDiagnostic>(),
        PolicyConsistencyConfig: "off",
        CoverageSummaries: Array.Empty<ArchitectureCoverageSummary>(),
        ClassificationConflicts: Array.Empty<ArchitectureClassificationConflict>(),
        ClassificationMetadataFailures: Array.Empty<ArchitectureClassificationMetadataFailure>());
}
