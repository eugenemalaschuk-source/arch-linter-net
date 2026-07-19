namespace ArchLinterNet.Core.Model;

public sealed record ContextDependencyDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.ContextDependency;

    public string? SourceRole { get; init; }
    public IReadOnlyDictionary<string, object>? SourceMetadata { get; init; }
    public string? TargetRole { get; init; }
    public IReadOnlyDictionary<string, object>? TargetMetadata { get; init; }
    public string? MatchedSelector { get; init; }
    public IReadOnlyList<ExpressionParticipation>? WhenExpressions { get; init; }
}
