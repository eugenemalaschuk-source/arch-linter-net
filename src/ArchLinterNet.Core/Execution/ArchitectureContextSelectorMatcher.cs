using System.Collections;
using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Matches a candidate type against an ArchitectureContextSelector's role + metadata-operator
// vocabulary. Distinct from ArchitectureLayerTypeMatcher, which stays pinned to exact/AND-only
// matching for layers.<name>.selector — see
// openspec/changes/add-contextual-dependency-contracts/design.md Decision 1 and Decision 2.
//
// Metadata value grammar, checked in this fixed order:
//   1. YAML sequence                                    -> in (matches any listed entry)
//   2. literal string "*"                                -> any (matches any resolved value, key must be present)
//   3. string matching ^!\{source\.metadata\.<key>\}$    -> not-equal-to-source (resolved against the
//      current source type's own metadata; only meaningful on forbidden/allowed/exclude selectors)
//   4. anything else                                     -> exact literal
internal static partial class ArchitectureContextSelectorMatcher
{
    [GeneratedRegex(@"^!\{source\.metadata\.([A-Za-z0-9_]+)\}$", RegexOptions.CultureInvariant)]
    private static partial Regex NotEqualToSourcePattern();

    // sourceDescriptor is the current contract evaluation's source type's own resolved role/metadata,
    // used to resolve not-equal-to-source constraints. Pass null when matching the contract's own
    // `source` selector itself (which has no other source to reference).
    public static bool Matches(
        ArchitectureContextSelector selector,
        Type candidateType,
        ArchitectureRoleIndex roleIndex,
        ArchitectureTypeClassificationResult? sourceDescriptor)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(candidateType);
        ArgumentNullException.ThrowIfNull(roleIndex);

        if (!roleIndex.TryGetRole(candidateType, out ArchitectureTypeClassificationResult descriptor)
            || !string.Equals(descriptor.Role, selector.Role, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (KeyValuePair<string, object> entry in selector.Metadata)
        {
            if (!MatchesMetadataConstraint(entry.Key, entry.Value, descriptor, sourceDescriptor))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesMetadataConstraint(
        string key,
        object constraintValue,
        ArchitectureTypeClassificationResult descriptor,
        ArchitectureTypeClassificationResult? sourceDescriptor)
    {
        if (!descriptor.Metadata.TryGetValue(key, out object? actual))
        {
            return false;
        }

        if (constraintValue is IEnumerable sequence and not string)
        {
            foreach (object item in sequence)
            {
                if (ArchitectureMetadataValueComparer.ValuesEqual(actual, item))
                {
                    return true;
                }
            }

            return false;
        }

        if (constraintValue is string stringValue)
        {
            if (stringValue == "*")
            {
                return true;
            }

            Match match = NotEqualToSourcePattern().Match(stringValue);
            if (match.Success)
            {
                string sourceKey = match.Groups[1].Value;
                if (sourceDescriptor == null
                    || !sourceDescriptor.Metadata.TryGetValue(sourceKey, out object? sourceValue))
                {
                    return false;
                }

                return !ArchitectureMetadataValueComparer.ValuesEqual(actual, sourceValue);
            }
        }

        return ArchitectureMetadataValueComparer.ValuesEqual(actual, constraintValue);
    }
}
