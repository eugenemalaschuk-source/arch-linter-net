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
    public IReadOnlyList<string>? ExpectedRoles { get; init; }
    public string? ActualRole { get; init; }
    public bool? ExpectedAbstractClass { get; init; }
    public bool? ActualIsAbstract { get; init; }
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
            ExpectedRoles = ExpectedRoles,
            ActualRole = ActualRole,
            ExpectedAbstractClass = ExpectedAbstractClass,
            ActualIsAbstract = ActualIsAbstract,
            ExpectedDeclarationCount = ExpectedDeclarationCount,
            ActualDeclarationCount = ActualDeclarationCount,
            DeclarationPaths = DeclarationPaths,
            DataUnavailable = DataUnavailable,
            WhenExpressions = WhenExpressions
        };
}
