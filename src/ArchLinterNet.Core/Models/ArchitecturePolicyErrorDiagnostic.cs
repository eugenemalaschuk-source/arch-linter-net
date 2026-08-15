namespace ArchLinterNet.Core.Model;

public sealed record ArchitecturePolicyErrorDiagnostic(
    string Message,
    ArchitecturePolicyDiagnosticKind DiagnosticKind,
    string? ErrorCategory,
    IReadOnlyList<string> ImportChain)
    : ArchitectureDiagnostic("architecture policy", null)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.ArchitecturePolicyError;
}
