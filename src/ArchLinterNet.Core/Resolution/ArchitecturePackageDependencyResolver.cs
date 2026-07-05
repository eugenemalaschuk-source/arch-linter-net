using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Resolution;

internal static class ArchitecturePackageDependencyResolver
{
    public static bool MatchesGroup(ArchitecturePackageGroup group, string packageId)
    {
        return EnumerableUsableEntries(group.PackageIds)
                   .Any(id => string.Equals(packageId, id, StringComparison.OrdinalIgnoreCase))
               || EnumerableUsableEntries(group.PackagePrefixes)
                   .Any(prefix => MatchesPrefix(packageId, prefix));
    }

    private static bool MatchesPrefix(string packageId, string prefix)
    {
        return string.Equals(packageId, prefix, StringComparison.OrdinalIgnoreCase)
               || packageId.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerableUsableEntries(IEnumerable<string> entries)
    {
        return entries.Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry.Trim());
    }
}
