namespace ArchLinterNet.Core.Model;

public sealed record LayoutConventionPayload(
    string? MatchedFilePath = null,
    string? ExpectedTypeKind = null,
    string? ActualTypeKind = null,
    string? ExpectedTypeName = null,
    string? ActualTypeName = null,
    string? ExpectedCounterpartName = null,
    bool DataUnavailable = false)
    : IArchitectureDiagnosticPayload
{
    public int? ExpectedDeclarationCount { get; init; }
    public int? ActualDeclarationCount { get; init; }
    public IReadOnlyList<string>? DeclarationPaths { get; init; }
    public IReadOnlyList<ExpressionParticipation>? WhenExpressions { get; init; }
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
            ExpectedDeclarationCount = ExpectedDeclarationCount,
            ActualDeclarationCount = ActualDeclarationCount,
            DeclarationPaths = DeclarationPaths,
            DataUnavailable = DataUnavailable,
            WhenExpressions = WhenExpressions
        };
}
