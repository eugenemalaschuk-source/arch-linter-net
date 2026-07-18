namespace ArchLinterNet.Core.Resolution;

// Shared suffix/prefix naming-convention check, used by both type_placement and layout_conventions
// (and any future contract family with the same required/forbidden suffix/prefix shape) so the
// four-branch match/describe logic exists in exactly one place instead of being copied per family.
internal static class ArchitectureNameConventionMatcher
{
    public static bool Matches(
        string name, string requiredSuffix, string requiredPrefix, string forbiddenSuffix, string forbiddenPrefix)
    {
        if (!string.IsNullOrEmpty(requiredSuffix) && !name.EndsWith(requiredSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(requiredPrefix) && !name.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(forbiddenSuffix) && name.EndsWith(forbiddenSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(forbiddenPrefix) && name.StartsWith(forbiddenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    public static string Describe(
        string requiredSuffix, string requiredPrefix, string forbiddenSuffix, string forbiddenPrefix)
    {
        List<string> parts = new();
        if (!string.IsNullOrEmpty(requiredSuffix))
        {
            parts.Add($"required_suffix: {requiredSuffix}");
        }

        if (!string.IsNullOrEmpty(requiredPrefix))
        {
            parts.Add($"required_prefix: {requiredPrefix}");
        }

        if (!string.IsNullOrEmpty(forbiddenSuffix))
        {
            parts.Add($"forbidden_suffix: {forbiddenSuffix}");
        }

        if (!string.IsNullOrEmpty(forbiddenPrefix))
        {
            parts.Add($"forbidden_prefix: {forbiddenPrefix}");
        }

        return string.Join("; ", parts);
    }
}
