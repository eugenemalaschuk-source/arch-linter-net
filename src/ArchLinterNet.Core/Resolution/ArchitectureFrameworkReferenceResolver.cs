using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Resolution;

internal static class ArchitectureFrameworkReferenceResolver
{
    public static bool MatchesGroup(ArchitectureFrameworkReferenceGroup group, string frameworkName)
    {
        return EnumerableUsableEntries(group.FrameworkNames)
                   .Any(name => string.Equals(frameworkName, name, StringComparison.OrdinalIgnoreCase))
               || EnumerableUsableEntries(group.FrameworkNamePrefixes)
                   .Any(prefix => MatchesPrefix(frameworkName, prefix));
    }

    public static bool HasUsableMatchers(ArchitectureFrameworkReferenceGroup group)
    {
        return EnumerableUsableEntries(group.FrameworkNames).Any()
               || EnumerableUsableEntries(group.FrameworkNamePrefixes).Any();
    }

    private static bool MatchesPrefix(string frameworkName, string prefix)
    {
        return string.Equals(frameworkName, prefix, StringComparison.OrdinalIgnoreCase)
               || frameworkName.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerableUsableEntries(IEnumerable<string> entries)
    {
        return entries.Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry.Trim());
    }
}
