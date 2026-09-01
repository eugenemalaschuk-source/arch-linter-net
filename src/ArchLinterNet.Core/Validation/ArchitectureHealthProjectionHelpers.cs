using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

internal static class ArchitectureHealthProjectionHelpers
{
    internal static ArchitectureHealthDimension Dimension(
        string name,
        ArchitectureHealthDimensionState state,
        params string[] reasonCodes) =>
        Dimension(name, state, (IEnumerable<string>)reasonCodes);

    internal static ArchitectureHealthDimension Dimension(
        string name,
        ArchitectureHealthDimensionState state,
        IEnumerable<string> reasonCodes) =>
        Dimension(name, state, reasonCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => new ArchitectureHealthReason(code, name)));

    internal static ArchitectureHealthDimension Dimension(
        string name,
        ArchitectureHealthDimensionState state,
        IEnumerable<ArchitectureHealthReason> reasons) =>
        new(
            name,
            state,
            reasons
                .Where(reason => !string.IsNullOrWhiteSpace(reason.Code))
                .Distinct()
                .ToArray());

    internal static ArchitectureHealthReason Reason(
        string code,
        string source,
        string? family = null,
        string? controlIdentity = null,
        string? policyIdentity = null,
        string? evidenceIdentity = null) =>
        new(code, source)
        {
            Family = family,
            ControlIdentity = controlIdentity,
            PolicyIdentity = policyIdentity,
            EvidenceIdentity = evidenceIdentity,
        };

    internal static ArchitectureHealthReason Reason(
        string source,
        ArchitectureApplicabilityReason reason) =>
        Reason(
            reason.Code,
            source,
            reason.Provenance.Family,
            reason.Provenance.ControlIdentity,
            reason.Provenance.PolicyIdentity);

    internal static string? PolicyIdentity(ArchitecturePolicySourceLocation? location) => location is null
        ? null
        : $"{location.SourcePath}:{location.YamlPath}";

    internal static string EvidenceIdentity(ArchitectureViolation violation) => violation.Identity is not null
        ? ArchitectureViolationIdentityJson.Serialize(violation.Identity)
        : string.Join(
            "|",
            violation.ContractId ?? violation.ContractName,
            violation.SourceType,
            violation.ForbiddenNamespace,
            string.Join(",", violation.ForbiddenReferences.OrderBy(reference => reference, StringComparer.Ordinal)));
}
