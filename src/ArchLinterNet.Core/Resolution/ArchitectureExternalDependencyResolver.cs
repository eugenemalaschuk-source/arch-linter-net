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
        return EnumerateUsablePrefixes(group.NamespacePrefixes)
                   .Any(prefix => ArchitectureLayerResolver.MatchesPrefix(namespaceName, prefix))
               || EnumerateUsablePrefixes(group.TypePrefixes)
                   .Any(prefix => MatchesTypePrefix(typeFullName, prefix));
    }

    public static bool HasUsableMatchers(ArchitectureExternalDependencyGroup group)
    {
        return EnumerateUsablePrefixes(group.NamespacePrefixes).Any()
               || EnumerateUsablePrefixes(group.TypePrefixes).Any();
    }

    private static bool MatchesTypePrefix(string typeFullName, string typePrefix)
    {
        return string.Equals(typeFullName, typePrefix, StringComparison.Ordinal)
               || typeFullName.StartsWith(typePrefix + ".", StringComparison.Ordinal)
               || typeFullName.StartsWith(typePrefix + "+", StringComparison.Ordinal)
               || typeFullName.StartsWith(typePrefix + "`", StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateUsablePrefixes(IEnumerable<string> prefixes)
    {
        return prefixes.Where(prefix => !string.IsNullOrWhiteSpace(prefix)).Select(prefix => prefix.Trim());
    }
}
