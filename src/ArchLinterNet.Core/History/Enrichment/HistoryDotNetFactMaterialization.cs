namespace ArchLinterNet.Core.History.Enrichment;

internal sealed class HistoryDotNetFactMaterialization(
    IReadOnlyDictionary<string, IReadOnlyList<HistoryDotNetTypeContext>> typesByCanonicalPath)
{
    public IReadOnlyDictionary<string, IReadOnlyList<HistoryDotNetTypeContext>> TypesByCanonicalPath { get; } =
        typesByCanonicalPath;
}
