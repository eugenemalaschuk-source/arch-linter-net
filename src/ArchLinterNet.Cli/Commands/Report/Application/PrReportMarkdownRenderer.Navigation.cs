using System.Text;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands.Report.Application;

using static PrReportMarkdownFormatter;

internal static class PrReportMarkdownNavigation
{
    internal static void AppendNavigation(
        StringBuilder builder,
        ArchitecturePrReportProjection projection,
        int maxDetails,
        ArchitecturePrReportNavigationContext? transportContext)
    {
        transportContext ??= TryGetProjectionTransportContext(projection);
        string? repositoryRoot = PrimaryReceipt(projection)?.Provenance.RepositoryRoot;
        builder.AppendLine("## Canonical navigation");
        List<string> references = projection.Navigation
            .OrderBy(item => item.Authority, StringComparer.Ordinal)
            .ThenBy(item => item.Identity, StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .Select(item => FormatNavigationReference(item, repositoryRoot, transportContext))
            .ToList();
        AppendBounded(builder, "References", references.Count, references, maxDetails,
            static item => $"- {item}");

        // This navigation is deliberately outside the ordinary bounded reference list. It is a
        // transport-only pointer to the immutable producer bundle, not report evidence.
        builder.AppendLine("### Full immutable report bundle");
        builder.AppendLine(!string.IsNullOrWhiteSpace(transportContext?.ArtifactUrl)
            ? $"- [Open full report bundle]({transportContext.ArtifactUrl})"
            : "- Full immutable report bundle: `unavailable`");
    }

    private static string FormatNavigationReference(
        ArchitecturePrReportNavigationReference reference,
        string? repositoryRoot,
        ArchitecturePrReportNavigationContext? transportContext)
    {
        string value = $"`{Inline(reference.Authority)}`" +
            (string.IsNullOrWhiteSpace(reference.Identity) ? string.Empty : $" `{Inline(reference.Identity)}`");
        string? relativePath = PrReportTransportContext.RepositoryRelativePath(reference.Path, repositoryRoot);
        if (relativePath is null)
        {
            return value;
        }

        string path = transportContext?.GetSourceUrl(relativePath) is { } blobUrl
            ? $"[{Text(relativePath)}]({blobUrl})"
            : Text(relativePath);
        return $"{value} ({path})";
    }

    private static ArchitecturePrReportNavigationContext? TryGetProjectionTransportContext(
        ArchitecturePrReportProjection projection)
    {
        ArchitecturePrReportNavigationContext? context = projection.NavigationContext;
        return context?.IsUsable == true ? context : null;
    }

    private static void AppendBounded<T>(
        StringBuilder builder,
        string title,
        int total,
        IReadOnlyList<T> items,
        int maxDetails,
        Func<T, string> format)
    {
        if (total <= 0 || items.Count == 0)
        {
            return;
        }

        builder.AppendLine($"### {title} ({total})");
        int shown = Math.Min(total, Math.Min(maxDetails, items.Count));
        builder.AppendLine($"Showing {shown} of {total}; omitted {Math.Max(0, total - shown)}.");
        for (int index = 0; index < shown; index++)
        {
            builder.AppendLine(format(items[index]));
        }
    }
}
