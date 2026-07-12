namespace ArchLinterNet.Core.Model;

public sealed record PortBoundaryPayload(
    string? SourceRole,
    IReadOnlyDictionary<string, object>? SourceMetadata,
    string? TargetRole,
    IReadOnlyDictionary<string, object>? TargetMetadata,
    string EvidenceKind, string ExpectedSeam) : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new PortBoundaryDiagnostic(violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            SourceRole = SourceRole,
            SourceMetadata = SourceMetadata,
            TargetRole = TargetRole,
            TargetMetadata = TargetMetadata,
            EvidenceKind = EvidenceKind,
            ExpectedSeam = ExpectedSeam
        };
}
