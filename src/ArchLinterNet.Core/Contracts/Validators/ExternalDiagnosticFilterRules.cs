namespace ArchLinterNet.Core.Contracts.Validators;

internal static class ExternalDiagnosticFilterRules
{
    public static readonly string[] SupportedSeverities =
    [
        "error", "warning", "note", "none", "unspecified",
    ];

    public static readonly string[] SupportedModes =
    [
        "strict", "audit",
    ];

    public static bool IsSafePathPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains('\\')
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith("~/", StringComparison.Ordinal)
            || value.Contains("//", StringComparison.Ordinal)
            || value.IndexOfAny(['*', '?', '[', ']']) >= 0
            || (value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':'))
        {
            return false;
        }

        string[] segments = value.Split('/');
        for (int index = 0; index < segments.Length; index++)
        {
            string segment = segments[index];
            if (index == segments.Length - 1 && segment.Length == 0)
            {
                // A trailing slash is the canonical spelling for a directory prefix and is
                // intentionally accepted (for example, `src/`).
                continue;
            }

            if (segment.Length == 0 || segment is "." or ".." || segment.Any(char.IsControl))
            {
                return false;
            }
        }

        return true;
    }
}
