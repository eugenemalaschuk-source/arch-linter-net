namespace ArchLinterNet.Core.Model;

public sealed record AttributeUsagePayload(
    string? MatchedAttribute = null,
    string? AttributeUsageKind = null,
    string? ExpectedAttributeLocation = null,
    string? ActualAttributeLocation = null)
    : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new AttributeUsageDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes,
            MatchedAttribute = MatchedAttribute,
            AttributeUsageKind = AttributeUsageKind,
            ExpectedAttributeLocation = ExpectedAttributeLocation,
            ActualAttributeLocation = ActualAttributeLocation
        };
}
