namespace ArchLinterNet.Core.Model;

public sealed record LayoutConventionDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.LayoutConvention;

    public string? MatchedFilePath { get; init; }
    public string? ExpectedTypeKind { get; init; }
    public string? ActualTypeKind { get; init; }
    public string? ExpectedTypeName { get; init; }
    public string? ActualTypeName { get; init; }
    public string? ExpectedCounterpartName { get; init; }
    public IReadOnlyList<string>? ExpectedRoles { get; init; }
    public string? ActualRole { get; init; }
    public bool? ExpectedAbstractClass { get; init; }
    public bool? ActualIsAbstract { get; init; }
    public int? ExpectedDeclarationCount { get; init; }
    public int? ActualDeclarationCount { get; init; }
    public IReadOnlyList<string>? DeclarationPaths { get; init; }
    public bool DataUnavailable { get; init; }
    public IReadOnlyList<ExpressionParticipation>? WhenExpressions { get; init; }
}
