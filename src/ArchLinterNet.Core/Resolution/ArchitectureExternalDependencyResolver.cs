using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Resolution;

internal static class ArchitectureExternalDependencyResolver
{
    public static ArchitectureExternalDependencyGroup ResolveGroup(
        ArchitectureContractDocument document,
        string contractName,
        string groupName)
    {
        if (!document.ExternalDependencies.TryGetValue(groupName, out ArchitectureExternalDependencyGroup? group))
        {
            throw new InvalidOperationException(
                $"Architecture contract '{contractName}' references unknown external dependency group '{groupName}'.");
        }

        return group;
    }

    public static bool MatchesGroup(ArchitectureExternalDependencyGroup group, string typeFullName, string namespaceName)
    {
        return group.NamespacePrefixes.Any(prefix => ArchitectureLayerResolver.MatchesPrefix(namespaceName, prefix))
               || group.TypePrefixes.Any(prefix => MatchesTypePrefix(typeFullName, prefix));
    }

    private static bool MatchesTypePrefix(string typeFullName, string typePrefix)
    {
        return string.Equals(typeFullName, typePrefix, StringComparison.Ordinal)
               || typeFullName.StartsWith(typePrefix + ".", StringComparison.Ordinal)
               || typeFullName.StartsWith(typePrefix + "+", StringComparison.Ordinal)
               || typeFullName.StartsWith(typePrefix + "`", StringComparison.Ordinal);
    }
}
