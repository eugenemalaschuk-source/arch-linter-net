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
    /// reader results for the same logical control are aggregated here: one evidence requirement can
    /// legitimately be satisfied by complementary trusted artifacts. Unknown logical identities stay
    /// visible as orphan records for the common evaluator.
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
            ProjectRecords(declared, readResults, selection));
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
    /// Projects each logical external-evidence control into one shared applicability record.
    /// </summary>
    /// <remarks>
    /// Trusted artifacts are aggregated by their declared logical identity. A result's
    /// <see cref="SarifEvidenceReadResult.Status"/> is the sole trust input here; artifacts and
    /// contexts are never re-read or revalidated. Missing mandatory selection for an authorized
    /// <c>require_matches</c> filter fails closed as a stale declaration.
    /// </remarks>
    public static IReadOnlyList<ArchitectureApplicabilityRecord> ProjectRecords(
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
        IReadOnlyDictionary<string, ArchitectureExternalEvidenceRequirement> requirementsById = declared
            .GroupBy(requirement => requirement.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        return ProjectRecords(readResults, selection, requirementsById);
    }

    /// <summary>
    /// Projects read results without requiring callers to pass declarations a second time.
    /// </summary>
    public static IReadOnlyList<ArchitectureApplicabilityRecord> ProjectRecords(
        IEnumerable<SarifEvidenceReadResult> readResults,
        SarifExternalDiagnosticSelectionResult? selection = null)
    {
        ArgumentNullException.ThrowIfNull(readResults);

        return ProjectRecords(
            readResults,
            selection,
            new Dictionary<string, ArchitectureExternalEvidenceRequirement>(StringComparer.Ordinal));
    }

    private static IReadOnlyList<ArchitectureApplicabilityRecord> ProjectRecords(
        IEnumerable<SarifEvidenceReadResult> readResults,
        SarifExternalDiagnosticSelectionResult? selection,
        IReadOnlyDictionary<string, ArchitectureExternalEvidenceRequirement> requirementsById)
    {
        HashSet<string> selectionMismatches = selection?.FilterMismatches
            .Where(mismatch => mismatch is not null)
            .Select(mismatch => mismatch.LogicalEvidenceId)
            .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> selectionProcessedLogicalEvidenceIds = selection?.ProcessedLogicalEvidenceIds
            .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        return readResults
            .Select(readResult =>
            {
                ArgumentNullException.ThrowIfNull(readResult);
                return readResult;
            })
            // One logical evidence control can deliberately federate multiple artifacts. The
            // common evaluator's identity is the policy control, so aggregate artifact trust at
            // this boundary instead of presenting each physical artifact as a duplicate control.
            .GroupBy(readResult => readResult.LogicalId, StringComparer.Ordinal)
            .Select(group => CreateRecord(
                group.Key,
                group.OrderBy(ReadResultSortKey, StringComparer.Ordinal).ToArray(),
                selectionProcessedLogicalEvidenceIds.Contains(group.Key),
                selectionMismatches.Contains(group.Key),
                RequiresSelection(group.Key, group, requirementsById)))
            .OrderBy(record => record.ControlIdentity, StringComparer.Ordinal)
            .ThenBy(record => record.State)
            .ThenBy(record => record.Reasons.Count == 0 ? string.Empty : record.Reasons[0].Code,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static ArchitectureApplicabilityRecord CreateRecord(
        string logicalId,
        IReadOnlyList<SarifEvidenceReadResult> readResults,
        bool selectionCompleted,
        bool hasSelectionMismatch,
        bool requiresSelection)
    {
        ArchitectureApplicabilityProvenance provenance = Provenance(logicalId);
        ArchitectureApplicabilityRecordState state;
        string? reasonCode = null;

        if (readResults.All(readResult => readResult.Status is SarifEvidenceTrustStatus.Valid
                or SarifEvidenceTrustStatus.OptionalNotConfigured
                or SarifEvidenceTrustStatus.MissingOptionalInput)
            && readResults.Any(readResult => readResult.Status == SarifEvidenceTrustStatus.Valid))
        {
            if (hasSelectionMismatch || requiresSelection && !selectionCompleted)
            {
                state = ArchitectureApplicabilityRecordState.Unassessable;
                reasonCode = ArchitectureApplicabilityReasonCodes.StaleDeclaration;
            }
            else
            {
                state = ArchitectureApplicabilityRecordState.Evaluable;
            }
        }
        else if (readResults.All(readResult => readResult.Status is SarifEvidenceTrustStatus.OptionalNotConfigured
                 or SarifEvidenceTrustStatus.MissingOptionalInput))
        {
            state = ArchitectureApplicabilityRecordState.NotApplicable;
        }
        else
        {
            SarifEvidenceReadResult failure = readResults.First(readResult =>
                readResult.Status is not SarifEvidenceTrustStatus.Valid
                and not SarifEvidenceTrustStatus.OptionalNotConfigured
                and not SarifEvidenceTrustStatus.MissingOptionalInput);
            state = ArchitectureApplicabilityRecordState.Unassessable;
            reasonCode = ReadStatusReasonCode(failure.Status);
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

    private static bool RequiresSelection(
        string logicalId,
        IEnumerable<SarifEvidenceReadResult> readResults,
        IReadOnlyDictionary<string, ArchitectureExternalEvidenceRequirement> requirementsById)
    {
        // The reader's detached authorization is authoritative. A current declaration is only
        // additive here, because a mutable caller must not weaken a previously authorized
        // require_matches obligation by passing a different requirement with the same logical ID.
        bool declaredRequireMatches = requirementsById.TryGetValue(logicalId,
            out ArchitectureExternalEvidenceRequirement? requirement)
            && requirement.DiagnosticFilter?.RequireMatches == true;
        return declaredRequireMatches
            || readResults.Any(readResult => readResult.Authorization?.DiagnosticFilter?.RequireMatches == true);
    }

    private static string ReadResultSortKey(SarifEvidenceReadResult readResult) =>
        readResult.Status + "|" + readResult.ArtifactPath + "|" + readResult.ArtifactSha256 + "|" + readResult.Detail;

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
