using System.Globalization;
using System.Text.Json;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed record RepositoryMetricsBadgeProjection(
    string Label,
    string Message,
    string Color,
    string LogoSvg,
    int ExitCode);

internal enum RepositoryMetricsBadgeKind
{
    SourceLines,
    Repository,
    Structure,
}

internal static class RepositoryMetricsBadgeProjector
{
    internal static RepositoryMetricsBadgeProjection Project(
        string input,
        RepositoryMetricsBadgeKind kind = RepositoryMetricsBadgeKind.SourceLines)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(input);
            JsonElement metricsElement = document.RootElement.TryGetProperty("repository_metrics", out JsonElement nested)
                ? nested
                : document.RootElement;
            RepositoryMetricsSnapshot metrics = RepositoryMetricsJson.Deserialize(metricsElement.GetRawText());
            if (!metrics.IsComplete)
            {
                return Unavailable(kind);
            }

            return kind switch
            {
                RepositoryMetricsBadgeKind.SourceLines when metrics.Size.SourceLines is int sourceLines =>
                    Complete("source lines", FormatSourceLines(sourceLines)),
                RepositoryMetricsBadgeKind.Repository when metrics.Size.Projects is int projects && metrics.Size.Types is int types =>
                    Complete("repository", $"{FormatCount(projects)} projects · {FormatCount(types)} types"),
                RepositoryMetricsBadgeKind.Structure when metrics.Coupling.DependencyCount is int dependencies
                    && metrics.Structure.LargestSccSize is int largestScc =>
                    Complete("structure", $"{FormatCount(dependencies)} deps · SCC {FormatCount(largestScc)}"),
                _ => Unavailable(kind),
            };
        }
        catch (JsonException)
        {
            return Unavailable(kind);
        }
        catch (ArgumentException)
        {
            return Unavailable(kind);
        }
    }

    private static RepositoryMetricsBadgeProjection Complete(string label, string message) => new(
        label,
        message,
        "blue",
        ArchLinterNetBadgeLogo.Svg,
        CliExitCodes.Success);

    private static RepositoryMetricsBadgeProjection Unavailable(RepositoryMetricsBadgeKind kind) => new(
        kind switch
        {
            RepositoryMetricsBadgeKind.Repository => "repository",
            RepositoryMetricsBadgeKind.Structure => "structure",
            _ => "source lines",
        },
        "unavailable",
        "lightgrey",
        ArchLinterNetBadgeLogo.Svg,
        CliExitCodes.InvalidArgumentsOrRuntimeError);

    private static string FormatCount(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string FormatSourceLines(int sourceLines)
    {
        if (sourceLines < 1000)
        {
            return sourceLines.ToString("N0", CultureInfo.InvariantCulture);
        }

        double value = sourceLines;
        string suffix;
        if (sourceLines >= 1_000_000)
        {
            value /= 1_000_000;
            suffix = "M";
        }
        else
        {
            value /= 1000;
            suffix = "k";
        }

        return value.ToString(value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00", CultureInfo.InvariantCulture) + suffix;
    }
}
