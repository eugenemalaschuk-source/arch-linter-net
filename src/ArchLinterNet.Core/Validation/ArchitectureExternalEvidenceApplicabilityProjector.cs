using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Projects external-evidence trust and selection outcomes into the shared applicability inputs.
/// </summary>
/// <remarks>
/// This adapter deliberately does not read SARIF, inspect the assessment context, or infer trust
/// from any property other than the reader's closed status. It also does not introduce a second
/// external-evidence applicability model: callers pass the returned collections directly to
/// <see cref="ArchitectureApplicabilityEvaluator"/>.
/// </remarks>
public static class ArchitectureExternalEvidenceApplicabilityProjector
{
    internal const string Family = "external_diagnostics";

    /// <summary>
    /// Projects declared external-evidence requirements and their read/selection outcomes into
    /// the shared expected entries and records used by the common evaluator.
    /// </summary>
    /// <remarks>
    /// The tuple is only a collection convenience; the projection's actual values are the
    /// ordinary shared applicability types. Inputs are not de-duplicated so the common evaluator
    /// can report duplicate or orphan identities through its existing integrity checks.
    /// </remarks>
    public static (
        IReadOnlyList<ArchitectureApplicabilityExpectedEntry> ExpectedEntries,
        IReadOnlyList<ArchitectureApplicabilityRecord> Records) Project(
        IEnumerable<ArchitectureExternalEvidenceRequirement> requirements,
        IEnumerable<SarifEvidenceReadResult> readResults,
        SarifExternalDiagnosticSelectionResult? selection = null)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(readResults);

        ArchitectureExternalEvidenceRequirement[] declared = requirements
            .Select(requirement =>
            {
                ArgumentNullException.ThrowIfNull(requirement);
                return requirement;
            })
            .ToArray();
        return (
            ProjectExpectedEntries(declared),
            ProjectRecords(readResults, selection));
    }

    /// <summary>Projects one expected shared-applicability entry for each declaration.</summary>
    public static IReadOnlyList<ArchitectureApplicabilityExpectedEntry> ProjectExpectedEntries(
        IEnumerable<ArchitectureExternalEvidenceRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        return requirements
            .Select(requirement =>
            {
                ArgumentNullException.ThrowIfNull(requirement);
                ArchitectureApplicabilityProvenance provenance = Provenance(requirement.Id);
                ArchitectureApplicabilityMembership membership = requirement.Required
                    ? ArchitectureApplicabilityMembership.Required
                    : ArchitectureApplicabilityMembership.Optional;
                return new ArchitectureApplicabilityExpectedEntry(
                    requirement.Id,
                    Family,
                    membership,
                    provenance);
            })
            .OrderBy(entry => entry.ControlIdentity, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Projects each supplied reader result into one shared applicability record.
    /// </summary>
    /// <remarks>
    /// Every supplied result is retained, including an identity not present in the declarations
    /// and repeated identities. The common evaluator owns those unknown/duplicate identity
    /// diagnostics. A result's <see cref="SarifEvidenceReadResult.Status"/> is the sole trust
    /// input here; the artifact and its context are never re-read or revalidated.
    /// </remarks>
    public static IReadOnlyList<ArchitectureApplicabilityRecord> ProjectRecords(
        IEnumerable<ArchitectureExternalEvidenceRequirement> requirements,
        IEnumerable<SarifEvidenceReadResult> readResults,
        SarifExternalDiagnosticSelectionResult? selection = null)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(readResults);

        // Validate declarations at this boundary even though record identity comes solely from
        // the reader result. The declaration list is used by the paired projection, while this
        // overload keeps the two collection projections convenient for callers that already have
        // both inputs.
        _ = requirements
            .Select(requirement =>
            {
                ArgumentNullException.ThrowIfNull(requirement);
                return requirement;
            })
            .ToArray();
        return ProjectRecords(readResults, selection);
    }

    /// <summary>
    /// Projects read results without requiring callers to pass declarations a second time.
    /// </summary>
    public static IReadOnlyList<ArchitectureApplicabilityRecord> ProjectRecords(
        IEnumerable<SarifEvidenceReadResult> readResults,
        SarifExternalDiagnosticSelectionResult? selection = null)
    {
        ArgumentNullException.ThrowIfNull(readResults);

        HashSet<string> selectionMismatches = selection?.FilterMismatches
            .Where(mismatch => mismatch is not null)
            .Select(mismatch => mismatch.LogicalEvidenceId)
            .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        return readResults
            .Select(readResult => CreateRecord(readResult, selectionMismatches))
            .OrderBy(record => record.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(record => record.State)
            .ThenBy(record => record.Reasons.Count == 0 ? string.Empty : record.Reasons[0].Code,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static ArchitectureApplicabilityRecord CreateRecord(
        SarifEvidenceReadResult readResult,
        IReadOnlySet<string> selectionMismatches)
    {
        ArgumentNullException.ThrowIfNull(readResult);

        string logicalId = readResult.LogicalId;
        ArchitectureApplicabilityProvenance provenance = Provenance(logicalId);
        ArchitectureApplicabilityRecordState state;
        string? reasonCode = null;

        switch (readResult.Status)
        {
            case SarifEvidenceTrustStatus.Valid:
                if (selectionMismatches.Contains(logicalId))
                {
                    state = ArchitectureApplicabilityRecordState.Unassessable;
                    reasonCode = ArchitectureApplicabilityReasonCodes.StaleDeclaration;
                }
                else
                {
                    state = ArchitectureApplicabilityRecordState.Evaluable;
                }

                break;

            case SarifEvidenceTrustStatus.OptionalNotConfigured:
            case SarifEvidenceTrustStatus.MissingOptionalInput:
                state = ArchitectureApplicabilityRecordState.NotApplicable;
                break;

            default:
                state = ArchitectureApplicabilityRecordState.Unassessable;
                reasonCode = ReadStatusReasonCode(readResult.Status);
                break;
        }

        IReadOnlyList<ArchitectureApplicabilityReason> reasons = reasonCode is null
            ? Array.Empty<ArchitectureApplicabilityReason>()
            : [new ArchitectureApplicabilityReason(reasonCode, provenance)];
        return new ArchitectureApplicabilityRecord(
            logicalId,
            Family,
            state,
            reasons,
            provenance);
    }

    private static string ReadStatusReasonCode(SarifEvidenceTrustStatus status) =>
        status switch
        {
            SarifEvidenceTrustStatus.MissingRequiredInput =>
                ArchitectureApplicabilityReasonCodes.MissingRequiredInput,
            SarifEvidenceTrustStatus.WrongLogicalId =>
                ArchitectureApplicabilityReasonCodes.WrongExternalEvidenceIdentity,
            SarifEvidenceTrustStatus.WrongRepository =>
                ArchitectureApplicabilityReasonCodes.WrongExternalRepository,
            SarifEvidenceTrustStatus.WrongRevision =>
                ArchitectureApplicabilityReasonCodes.WrongExternalRevision,
            SarifEvidenceTrustStatus.WrongScope =>
                ArchitectureApplicabilityReasonCodes.WrongExternalScope,
            _ => ArchitectureApplicabilityReasonCodes.MalformedExternalInput,
        };

    private static ArchitectureApplicabilityProvenance Provenance(string logicalId) =>
        new(Family, logicalId, logicalId);
}
