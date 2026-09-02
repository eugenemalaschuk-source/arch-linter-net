using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Composes the existing external-evidence trust/selection/normalization/applicability chain
/// (<see cref="SarifEvidenceReader"/>, <see cref="SarifExternalDiagnosticSelector"/>,
/// <see cref="ArchitectureImportedDiagnosticProjector"/>,
/// <see cref="ArchitectureExternalEvidenceApplicabilityProjector"/>) into one reusable call for a
/// caller — such as the packed CLI — that supplies repository-local artifact bindings rather than
/// running the chain by hand.
/// </summary>
/// <remarks>
/// This boundary deliberately reimplements none of the chain's own trust, selection, normalization,
/// or applicability semantics. It only matches a caller's supplied bindings to the policy's declared
/// requirements by logical id, calls the existing pipeline once per requirement, and merges the
/// already-produced results into a <see cref="ValidationOutcome"/>.
/// </remarks>
public static class ArchitectureExternalEvidenceBinder
{
    /// <summary>
    /// Reads, selects, and projects every declared requirement against its bound artifact (or
    /// <c>null</c> when unbound), letting the existing reader decide missing/optional outcomes.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A supplied artifact's logical id does not match any declared requirement, or two supplied
    /// artifacts share the same logical id. Both are caller (invocation) errors, not evidence-trust
    /// outcomes — the declared requirement set is the sole authority for which logical ids are valid.
    /// </exception>
    public static ArchitectureExternalEvidenceBindingResult Evaluate(
        IReadOnlyList<ArchitectureExternalEvidenceRequirement> requirements,
        string repositoryRoot,
        IReadOnlyList<SarifEvidenceArtifactReference> artifacts,
        SarifEvidenceAssessmentContext? assessmentContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(artifacts);

        if (requirements.Count == 0 && artifacts.Count == 0)
        {
            return ArchitectureExternalEvidenceBindingResult.Empty;
        }

        Dictionary<string, SarifEvidenceArtifactReference> artifactsById = IndexArtifacts(
            requirements, artifacts);
        return EvaluateBound(requirements, repositoryRoot, artifactsById, assessmentContext, cancellationToken);
    }

    /// <summary>
    /// Validates every supplied artifact's logical id against the declared requirements — the same
    /// check <see cref="Evaluate"/> performs before it reads any artifact bytes — without touching
    /// the filesystem. A caller that must reject an invalid binding before it is safe to attempt
    /// evidence I/O at all (for example, a run whose repository/build-state preflight has already
    /// failed for an unrelated reason) calls this directly instead of <see cref="Evaluate"/>, so a
    /// mistyped id is never silently dropped merely because the read step itself was skipped.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A supplied artifact's logical id does not match any declared requirement, or two supplied
    /// artifacts share the same logical id.
    /// </exception>
    public static void ValidateBindingIds(
        IReadOnlyList<ArchitectureExternalEvidenceRequirement> requirements,
        IReadOnlyList<SarifEvidenceArtifactReference> artifacts)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(artifacts);
        IndexArtifacts(requirements, artifacts);
    }

    private static ArchitectureExternalEvidenceBindingResult EvaluateBound(
        IReadOnlyList<ArchitectureExternalEvidenceRequirement> requirements,
        string repositoryRoot,
        Dictionary<string, SarifEvidenceArtifactReference> artifactsById,
        SarifEvidenceAssessmentContext? assessmentContext,
        CancellationToken cancellationToken)
    {
        var reader = new SarifEvidenceReader();
        List<SarifEvidenceReadResult> reads = new(requirements.Count);
        foreach (ArchitectureExternalEvidenceRequirement requirement in requirements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            artifactsById.TryGetValue(requirement.Id, out SarifEvidenceArtifactReference? artifact);
            reads.Add(reader.Read(
                requirement, repositoryRoot, artifact, assessmentContext, cancellationToken: cancellationToken));
        }

        SarifExternalDiagnosticSelectionInput[] selectable = reads
            .Where(read => read.IsValid && read.Authorization is not null)
            .Select(read => new SarifExternalDiagnosticSelectionInput(read))
            .ToArray();
        SarifExternalDiagnosticSelectionResult selection = selectable.Length == 0
            ? new SarifExternalDiagnosticSelectionResult()
            : new SarifExternalDiagnosticSelector().Select(selectable, cancellationToken);

        ImportedExternalDiagnosticProjection imported = ArchitectureImportedDiagnosticProjector.Project(
            selection, cancellationToken);
        (IReadOnlyList<ArchitectureApplicabilityExpectedEntry> expected,
            IReadOnlyList<ArchitectureApplicabilityRecord> records) =
            ArchitectureExternalEvidenceApplicabilityProjector.Project(requirements, reads, selection);

        return new ArchitectureExternalEvidenceBindingResult(imported, expected, records)
        {
            TrustReceipts = Array.AsReadOnly(reads.ToArray()),
        };
    }

    /// <summary>
    /// Merges an evaluated binding into a <see cref="ValidationOutcome"/>: folds its applicability
    /// expected entries/records into the outcome's existing collections, recomputes completion and
    /// projection through the same shared evaluator/projector every other applicability producer
    /// uses, re-derives the effective pass state, and attaches the imported diagnostics.
    /// </summary>
    /// <remarks>
    /// The pass-state formula (<c>ordinaryPassed &amp;&amp; completion?.State is not (Fail or
    /// Unassessable)</c>) mirrors <c>ArchitectureAnalysisSnapshot.Applicability.cs</c>'s own private
    /// <c>HasPassedAssessment</c> exactly; it is duplicated here rather than shared because that
    /// method is a private member of a different partial-class family. <paramref name="outcome"/>'s
    /// own <see cref="ValidationOutcome.NativePassed"/> stands in for the snapshot's local
    /// <c>ordinaryPassed</c> — the two are equal whenever no other applicability producer has already
    /// run, which holds for every caller of this method today.
    /// </remarks>
    public static ValidationOutcome Attach(
        ValidationOutcome outcome, ArchitectureExternalEvidenceBindingResult binding, string? mode)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(binding);

        if (binding.ApplicabilityExpectedEntries.Count == 0
            && binding.ApplicabilityRecords.Count == 0
            && binding.ImportedDiagnostics.Findings.Count == 0
            && binding.TrustReceipts.Count == 0)
        {
            return outcome;
        }

        IReadOnlyList<ArchitectureApplicabilityExpectedEntry> expected =
            outcome.ApplicabilityExpectedEntries.Concat(binding.ApplicabilityExpectedEntries).ToArray();
        IReadOnlyList<ArchitectureApplicabilityRecord> records =
            outcome.ApplicabilityRecords.Concat(binding.ApplicabilityRecords).ToArray();
        ArchitectureAssessmentCompletionEvidence? completion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected, records, outcome.NativePassed);
        ArchitectureApplicabilityProjection? projection = ArchitectureApplicabilityProjector.Project(
            completion, mode);
        bool passed = outcome.NativePassed
            && completion?.State is not (ArchitectureAssessmentCompletionState.Fail
                or ArchitectureAssessmentCompletionState.Unassessable);

        ValidationOutcome merged = outcome with
        {
            Passed = passed,
            ApplicabilityExpectedEntries = expected,
            ApplicabilityRecords = records,
            AssessmentCompletionEvidence = completion,
            ApplicabilityProjection = projection,
            ExternalEvidenceTrustReceipts = outcome.ExternalEvidenceTrustReceipts
                .Concat(binding.TrustReceipts)
                .ToArray(),
        };
        return merged.WithImportedDiagnostics(binding.ImportedDiagnostics);
    }

    private static Dictionary<string, SarifEvidenceArtifactReference> IndexArtifacts(
        IReadOnlyList<ArchitectureExternalEvidenceRequirement> requirements,
        IReadOnlyList<SarifEvidenceArtifactReference> artifacts)
    {
        HashSet<string> declaredIds = requirements
            .Select(requirement => requirement.Id)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, SarifEvidenceArtifactReference> artifactsById = new(StringComparer.Ordinal);
        foreach (SarifEvidenceArtifactReference artifact in artifacts)
        {
            if (!declaredIds.Contains(artifact.LogicalId))
            {
                throw new ArgumentException(
                    $"External evidence binding '{artifact.LogicalId}' does not match a declared " +
                    "external_evidence requirement.",
                    nameof(artifacts));
            }

            if (!artifactsById.TryAdd(artifact.LogicalId, artifact))
            {
                throw new ArgumentException(
                    $"Duplicate external evidence binding for logical id '{artifact.LogicalId}'.",
                    nameof(artifacts));
            }
        }

        return artifactsById;
    }
}

/// <summary>The composed result of evaluating every declared external-evidence requirement.</summary>
public sealed record ArchitectureExternalEvidenceBindingResult(
    ImportedExternalDiagnosticProjection ImportedDiagnostics,
    IReadOnlyList<ArchitectureApplicabilityExpectedEntry> ApplicabilityExpectedEntries,
    IReadOnlyList<ArchitectureApplicabilityRecord> ApplicabilityRecords)
{
    // The public binding result pre-dates PR reporting. Keep this receipt internal to preserve
    // that public contract while retaining the existing reader authority through Health output.
    internal IReadOnlyList<SarifEvidenceReadResult> TrustReceipts { get; init; } =
        Array.Empty<SarifEvidenceReadResult>();

    /// <summary>The result for a policy with no declared requirements and no supplied bindings.</summary>
    public static ArchitectureExternalEvidenceBindingResult Empty { get; } = new(
        ImportedExternalDiagnosticProjection.Empty,
        Array.Empty<ArchitectureApplicabilityExpectedEntry>(),
        Array.Empty<ArchitectureApplicabilityRecord>());
}
