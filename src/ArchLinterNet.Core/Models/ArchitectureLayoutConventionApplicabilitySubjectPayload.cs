namespace ArchLinterNet.Core.Model;

// Native subject-level evidence complements the inventory's scope-level control. It stays
// internal because callers consume it through normalized diagnostics, never through raw payloads.
internal sealed record ArchitectureLayoutConventionApplicabilitySubjectPayload(
    string SubjectIdentity,
    string ReasonCode,
    ArchitectureApplicabilityProvenance Provenance) : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new ArchitectureApplicabilityDiagnostic(
            violation.ContractName,
            violation.ContractId,
            SubjectIdentity,
            Provenance.Family,
            ArchitectureApplicabilityMembership.Required,
            ArchitectureApplicabilityRecordState.Unassessable,
            ArchitectureApplicabilityRecordState.Unassessable,
            new ArchitectureApplicabilityReason(ReasonCode, Provenance),
            Provenance);
}
