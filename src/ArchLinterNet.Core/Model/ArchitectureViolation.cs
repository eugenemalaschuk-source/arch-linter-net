namespace ArchLinterNet.Core.Model;

public sealed record ArchitectureViolation(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
{
    public IReadOnlyCollection<string>? MatchedNamespacePrefixes { get; init; }

    public IArchitectureDiagnosticPayload? Payload { get; init; }
}
