namespace ArchLinterNet.Core.Discovery;

// The single spelling of "same project path" used by everything that compares a policy-authored
// project path against a discovered one: the project_metadata checker, the configuration-reference
// collector, and CheckConfiguration's missing-project diagnostic. Path comparison itself stays
// case-insensitive at each call site; this only normalizes separators and surrounding whitespace.
internal static class ProjectPathNormalizer
{
    public static string Normalize(string path)
    {
        return path.Replace('\\', '/').Trim();
    }
}
