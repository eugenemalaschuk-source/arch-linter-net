using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    /// <summary>
    /// Additive overload carrying the resolved source-set expansion, so a JSON consumer can prove
    /// which sources an authored contract expanded to without parsing display text. Mirrors the
    /// cycle-diagnostics overload it extends; <c>sourceExpansion</c> is required (no default) so
    /// this overload stays unambiguous by arity against every prior one.
    /// </summary>
    public static string FormatResultForCiArtifacts( // NOSONAR: each parameter represents a semantically distinct section of the CI artifact payload; grouping would obscure the data contract
        string mode,
        bool passed,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        IReadOnlyCollection<ArchitectureCycleFinding> cycleFindings,
        IReadOnlyCollection<ArchitectureClassificationRoleFact> classificationRoles,
        ArchitectureClassificationPathDeferredNotice? classificationPathDeferred,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        ArchitectureSourceExpansionInventory sourceExpansion,
        IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
        IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
        IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        IReadOnlyCollection<ArchitectureClassificationConflict>? classificationConflicts = null,
        IReadOnlyCollection<ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null,
        IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? subtractiveMatcherParticipation = null)
    {
        return BuildCiArtifactsJson(new CiArtifactsRequest(
            mode, passed, violations, cycles, classificationRoles, coverageFindings, unmatched,
            policyConsistencyFindings, coverageSummaries, cycleFindings, classificationConflicts,
            classificationMetadataFailures, classificationPathDeferred, preflightDiagnostics)
        {
            SourceExpansion = sourceExpansion,
            SubtractiveMatcherParticipation = subtractiveMatcherParticipation
        });
    }
}
