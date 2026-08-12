using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Matches a candidate type against a public_api_surface contract's surface_selector (issue #525).
// Delegates entirely to the two existing matcher engines rather than forking matching behavior:
// structural fields (name/namespace/layer/base_type/implements_interface/has_attribute) reuse
// ArchitectureTypeRoleMatcher (the same engine type_placement.types_matching uses); Role reuses
// ArchitectureContextSelectorMatcher.MatchesLiteral via the existing semantic role index. Every
// populated field AND-combines, mirroring types_matching's own convention. See
// openspec/changes/add-public-api-surface-selector/design.md Decision 1.
internal static class ArchitecturePublicApiSurfaceSelectorMatcher
{
    public static bool Matches(
        Type type,
        ArchitecturePublicApiSurfaceSelector selector,
        ArchitectureContractDocument document,
        string contractName,
        ArchitectureRoleIndex roleIndex)
    {
        ArchitectureTypeMatcher structural = new()
        {
            NameSuffix = selector.NameSuffix,
            NamePrefix = selector.NamePrefix,
            Namespace = selector.Namespace,
            Layer = selector.Layer,
            BaseType = selector.BaseType,
            ImplementsInterface = selector.ImplementsInterface,
            HasAttribute = selector.HasAttribute,
        };

        if (!ArchitectureTypeRoleMatcher.Matches(type, structural, document, contractName))
        {
            return false;
        }

        if (string.IsNullOrEmpty(selector.Role))
        {
            return true;
        }

        ArchitectureContextSelector roleSelector = new() { Role = selector.Role };
        return ArchitectureContextSelectorMatcher.MatchesLiteral(roleSelector, type, roleIndex, sourceDescriptor: null);
    }
}
