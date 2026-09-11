using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands.Report.Application;

/// <summary>Validates producer transport arguments before creating the Core projection.</summary>
/// <remarks>
/// Core owns the navigation context and its allowlist. This CLI helper only enforces option
/// completeness, converts the values to the Core contract, and normalizes repository paths for
/// presentation. It never treats transport metadata as report evidence.
/// </remarks>
internal static class PrReportTransportContext
{
    public static bool TryCreate(
        string? repositoryUrl,
        string? headSha,
        string? artifactUrl,
        out ArchitecturePrReportNavigationContext? context,
        out string? error)
    {
        context = null;
        error = null;

        bool hasRepository = !string.IsNullOrWhiteSpace(repositoryUrl);
        bool hasHeadSha = !string.IsNullOrWhiteSpace(headSha);
        bool hasArtifact = !string.IsNullOrWhiteSpace(artifactUrl);
        if (!hasRepository && !hasHeadSha && !hasArtifact)
        {
            return true;
        }

        if (!hasRepository || !hasHeadSha)
        {
            error = "Report pr transport navigation requires --repository-url and --head-sha together.";
            return false;
        }

        ArchitecturePrReportNavigationContext candidate = new(
            repositoryUrl!.Trim(),
            headSha!.Trim(),
            hasArtifact ? artifactUrl!.Trim() : null);
        if (!candidate.IsUsable)
        {
            error = hasArtifact
                ? "Report pr transport navigation requires an HTTPS GitHub repository URL, a 40-character head SHA, and a matching GitHub Actions run or artifact URL."
                : "Report pr transport navigation requires an HTTPS GitHub repository URL and a 40-character head SHA.";
            return false;
        }

        context = candidate;
        return true;
    }

    /// <summary>Converts a canonical source path to a safe repository-relative path.</summary>
    public static string? RepositoryRelativePath(string? value, string? repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim().Replace('\\', '/');
        if (normalized.Contains("://", StringComparison.Ordinal)
            || normalized.Any(char.IsControl))
        {
            return null;
        }

        string? sourcePath = SourcePathPart(normalized);
        if (sourcePath is null)
        {
            return null;
        }

        normalized = sourcePath;
        string root = (repositoryRoot ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.IsNullOrEmpty(root))
        {
            if (string.Equals(normalized, root, comparison))
            {
                return null;
            }

            string prefix = root + "/";
            if (normalized.StartsWith(prefix, comparison))
            {
                normalized = normalized[prefix.Length..];
            }
            else if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                return null;
            }
        }
        else if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            return null;
        }

        if (normalized.Contains(":", StringComparison.Ordinal))
        {
            return null;
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            return null;
        }

        return string.Join("/", segments);
    }

    private static string? SourcePathPart(string normalized)
    {
        int separator = normalized.IndexOf(':');
        if (separator < 0)
        {
            return normalized;
        }

        // Windows drive prefixes are part of the path. Other colons are the canonical
        // policy-location suffix emitted by Core (path:/yaml/location).
        if (separator == 1 && char.IsLetter(normalized[0]))
        {
            int locationSeparator = normalized.IndexOf(':', 2);
            return locationSeparator < 0 ? normalized : normalized[..locationSeparator];
        }

        return separator == 0 ? null : normalized[..separator];
    }
}
