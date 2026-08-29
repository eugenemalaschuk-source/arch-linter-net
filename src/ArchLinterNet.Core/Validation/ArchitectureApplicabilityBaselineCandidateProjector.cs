using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

// Applicability findings use the same structured identity as every other normalized finding. This
// adapter only supplies the baseline envelope around that identity: the owning contract group is
// resolved from the effective catalog for the mode that produced the finding. In particular, it
// never manufactures an applicability-specific identity or a synthetic baseline group.
internal static class ArchitectureApplicabilityBaselineCandidateProjector
{
    internal static IReadOnlyList<ArchitectureBaselineCandidate> Project(
        ArchitectureContractDocument document,
        string mode,
        ArchitectureApplicabilityProjection? projection)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);

        return projection is null
            ? Array.Empty<ArchitectureBaselineCandidate>()
            : Project(document, mode, projection.Findings);
    }

    internal static IReadOnlyList<ArchitectureBaselineCandidate> Project(
        ArchitectureContractDocument document,
        string mode,
        IReadOnlyList<ArchitectureFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(findings);

        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);
        HashSet<string> baselineCapableGroups = catalog.BaselineCapableGroups()
            .ToHashSet(StringComparer.Ordinal);
        ArchitectureContractDescriptor[] descriptors = catalog.Descriptors
            .Where(descriptor => descriptor.Mode == mode
                && descriptor.Id is not null
                && baselineCapableGroups.Contains(descriptor.Group))
            .OrderBy(descriptor => descriptor.Group, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Family, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(descriptor => descriptor.Name, StringComparer.Ordinal)
            .ToArray();

        var projected = new List<ArchitectureBaselineCandidate>();
        var seenIdentities = new HashSet<string>(StringComparer.Ordinal);

        // Projection findings are already ordered by their authoritative projector. Reapply the
        // public finding order here because this helper is also a direct, internal test seam and
        // must not depend on the caller's collection order.
        foreach (ArchitectureFinding finding in ArchitectureFindingMapper.Order(findings))
        {
            if (finding.Details is not ArchitectureApplicabilityDiagnostic
                || finding.Identity is not { } identity
                || finding.ContractId is not { } contractId
                || identity.IdentityVersion != ArchitectureViolationIdentity.CurrentVersion
                || string.IsNullOrWhiteSpace(identity.ContractFamily)
                || string.IsNullOrWhiteSpace(identity.SourceType)
                || string.IsNullOrWhiteSpace(identity.TargetMember)
                || !string.Equals(identity.ContractId, contractId, StringComparison.Ordinal))
            {
                continue;
            }

            ArchitectureContractDescriptor? owner = descriptors.FirstOrDefault(descriptor =>
                string.Equals(descriptor.Family, identity.ContractFamily, StringComparison.Ordinal)
                && string.Equals(descriptor.Id, contractId, StringComparison.OrdinalIgnoreCase));
            if (owner is null)
            {
                // Generic/unknown applicability families remain output-only until a normal,
                // baseline-capable family descriptor exists in the policy catalog.
                continue;
            }

            string canonicalIdentity = ArchitectureViolationIdentityJson.Serialize(identity);
            if (!seenIdentities.Add(canonicalIdentity))
            {
                continue;
            }

            // Applicability's display pair is deliberately sourced from its identity: source_type
            // is the effective control and forbidden_reference is the canonical reason.
            projected.Add(new ArchitectureBaselineCandidate(
                owner.Group,
                contractId,
                identity.SourceType,
                identity.TargetMember,
                identity));
        }

        return projected
            .OrderBy(candidate => candidate.ContractGroup, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.ContractId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.SourceType, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.ForbiddenReference, StringComparer.Ordinal)
            .ThenBy(candidate => ArchitectureViolationIdentityJson.Serialize(candidate.Identity!), StringComparer.Ordinal)
            .ToArray();
    }
}
