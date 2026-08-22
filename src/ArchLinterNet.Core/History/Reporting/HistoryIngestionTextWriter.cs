using System.Globalization;
using System.Text;
using ArchLinterNet.Core.History.Analysis;

namespace ArchLinterNet.Core.History.Reporting;

// A deterministic human-readable summary of the same evidence. It is a convenience view, not a second
// canonical artifact: canonical report identity is over the JSON bytes.
internal static class HistoryIngestionTextWriter
{
    public static string Write(HistoryIngestionResult result)
    {
        StringBuilder text = new();
        Append(text, $"object format: {result.ObjectFormatName}");
        Append(text, $"from: {result.AuthoredFrom} -> {result.ResolvedFrom}");
        Append(text, $"to: {result.AuthoredTo} -> {result.ResolvedTo}");
        Append(text, $"commits: {result.Commits.Count.ToString(CultureInfo.InvariantCulture)} (excluded merges: {result.ExcludedMergeCount.ToString(CultureInfo.InvariantCulture)})");
        Append(text, $"rename candidates: {result.RenameCandidates.Count.ToString(CultureInfo.InvariantCulture)}");
        Append(text, $"logical files: {result.LogicalFiles.Count.ToString(CultureInfo.InvariantCulture)}");
        foreach (LogicalFile file in result.LogicalFiles)
        {
            string aliases = file.Aliases.Count == 0 ? string.Empty : $" (aliases: {string.Join(", ", file.Aliases)})";
            Append(text, $"  {file.CanonicalPath}{aliases}: commits={file.CommitCount.ToString(CultureInfo.InvariantCulture)} additions={file.Additions.ToString(CultureInfo.InvariantCulture)} deletions={file.Deletions.ToString(CultureInfo.InvariantCulture)} churn={file.Churn.ToString(CultureInfo.InvariantCulture)}");
        }

        return text.ToString();
    }

    private static void Append(StringBuilder text, string line) => text.Append(line).Append('\n');

}
