using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    // Reads the deduplicated conflict/evidence-extraction-failure facts from RoleIndex's single
    // cached extraction pass, for CLI/CI surfacing. RoleIndex computes these lazily on first access
    // and reuses them for the life of the session, so repeated calls no longer re-run extraction.
    public (IReadOnlyList<ArchitectureClassificationConflict> Conflicts, IReadOnlyList<ArchitectureClassificationMetadataFailure> MetadataFailures)
        CheckClassificationFacts()
    {
        return (RoleIndex.Conflicts, RoleIndex.MetadataFailures);
    }

    // Every classified type's discovered role, sorted by subject for deterministic CLI/CI output.
    public IReadOnlyList<ArchitectureClassificationRoleFact> CheckClassificationRoles()
    {
        List<ArchitectureClassificationRoleFact> roles = new();

        foreach (Type type in RoleIndex.ClassifiedTypes())
        {
            if (!RoleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor))
            {
                continue;
            }

            roles.Add(new ArchitectureClassificationRoleFact(
                ArchitectureTypeNames.SafeFullName(type), descriptor.Role!, descriptor.Source!.Value, descriptor.Metadata));
        }

        roles.Sort((a, b) => string.CompareOrdinal(a.Subject, b.Subject));
        return roles;
    }
}
