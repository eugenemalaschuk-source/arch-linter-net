using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal static class ArchitectureLayerTypeMatcher
{
    public static bool Matches(ArchitectureLayer layer, Type type, ArchitectureRoleIndex roleIndex)
    {
        if (!string.IsNullOrWhiteSpace(layer.Namespace)
            && !Resolution.ArchitectureLayerResolver.MatchesNamespace(
                layer, ArchitectureTypeNames.SafeNamespace(type)))
        {
            return false;
        }

        if (layer.Selector == null)
        {
            return !string.IsNullOrWhiteSpace(layer.Namespace);
        }

        if (!roleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor)
            || !string.Equals(descriptor.Role, layer.Selector.Role, StringComparison.Ordinal))
        {
            return false;
        }

        return layer.Selector.Metadata.All(entry =>
            descriptor.Metadata.TryGetValue(entry.Key, out object? actual)
            && ArchitectureMetadataValueComparer.ValuesEqual(actual, entry.Value));
    }
}
