using System.Reflection;
using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureSiblingGraphBuilder
{
    public static Dictionary<string, List<Type>> BuildSiblingGroups(
        IEnumerable<Assembly> targetAssemblies,
        string ancestorNamespace)
    {
        var groups = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
        string prefix = ancestorNamespace + ".";

        foreach (Assembly assembly in targetAssemblies.Distinct())
        {
            foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly))
            {
                string ns = ArchitectureTypeNames.SafeNamespace(type);
                if (string.IsNullOrEmpty(ns) || !ns.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string remainder = ns[prefix.Length..];
                int dotIndex = remainder.IndexOf('.');
                string child = dotIndex < 0 ? remainder : remainder[..dotIndex];

                if (!groups.TryGetValue(child, out List<Type>? types))
                {
                    types = new List<Type>();
                    groups[child] = types;
                }

                types.Add(type);
            }
        }

        return groups;
    }
}
