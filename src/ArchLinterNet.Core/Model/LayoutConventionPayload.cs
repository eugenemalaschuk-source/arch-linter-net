namespace ArchLinterNet.Core.Model;

public sealed record LayoutConventionPayload(
    string? MatchedFilePath = null,
    string? ExpectedTypeKind = null,
    string? ActualTypeKind = null,
    string? ExpectedTypeName = null,
    string? ActualTypeName = null,
    string? ExpectedCounterpartName = null,
    bool DataUnavailable = false,
    IReadOnlyList<ExpressionParticipation>? WhenExpressions = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new LayoutConventionDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            MatchedFilePath = MatchedFilePath,
            ExpectedTypeKind = ExpectedTypeKind,
            ActualTypeKind = ActualTypeKind,
            ExpectedTypeName = ExpectedTypeName,
            ActualTypeName = ActualTypeName,
            ExpectedCounterpartName = ExpectedCounterpartName,
            DataUnavailable = DataUnavailable,
            WhenExpressions = WhenExpressions
        };
}
