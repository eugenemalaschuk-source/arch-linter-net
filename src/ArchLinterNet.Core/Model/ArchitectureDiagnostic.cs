namespace ArchLinterNet.Core.Model;

public abstract record ArchitectureDiagnostic(string ContractName, string? ContractId)
{
    public abstract ArchitectureDiagnosticKind Kind { get; }

    public IReadOnlyCollection<string>? MatchedNamespacePrefixes { get; init; }
}
