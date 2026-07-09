namespace ArchLinterNet.Core.Model;

public sealed record ConfigurationPayload(
    string? TemplateName = null,
    string? ContainerNamespace = null,
    IReadOnlyCollection<IReadOnlyCollection<string>>? DependencyPaths = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new ConfigurationDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            TemplateName = TemplateName,
            ContainerNamespace = ContainerNamespace,
            DependencyPaths = DependencyPaths
        };
}
