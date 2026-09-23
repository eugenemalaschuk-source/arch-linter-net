using System.Globalization;
using System.Text.Json;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed record RepositoryMetricsBadgeProjection(
    string Message,
    string Color,
    int ExitCode);

internal static class RepositoryMetricsBadgeProjector
{
    internal static RepositoryMetricsBadgeProjection Project(string input)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(input);
            JsonElement metricsElement = document.RootElement.TryGetProperty("repository_metrics", out JsonElement nested)
                ? nested
                : document.RootElement;
            RepositoryMetricsSnapshot metrics = RepositoryMetricsJson.Deserialize(metricsElement.GetRawText());
            if (!metrics.IsComplete || metrics.Size.SourceLines is null)
            {
                return new("unavailable", "lightgrey", CliExitCodes.InvalidArgumentsOrRuntimeError);
            }

            return new(
                FormatSourceLines(metrics.Size.SourceLines.Value),
                "blue",
                CliExitCodes.Success);
        }
        catch (JsonException)
        {
            return new("unavailable", "lightgrey", CliExitCodes.InvalidArgumentsOrRuntimeError);
        }
        catch (ArgumentException)
        {
            return new("unavailable", "lightgrey", CliExitCodes.InvalidArgumentsOrRuntimeError);
        }
    }

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
