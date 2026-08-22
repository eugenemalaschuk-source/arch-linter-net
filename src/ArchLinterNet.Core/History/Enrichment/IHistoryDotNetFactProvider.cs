namespace ArchLinterNet.Core.History.Enrichment;

// The optional build/source step has a different failure boundary from canonical history ingestion.
// This narrow seam isolates that boundary and makes its deterministic projection independently testable.
internal interface IHistoryDotNetFactProvider
{
    HistoryDotNetFactMaterialization Materialize(string repositoryPath, string resolvedTo, string policyPath);
}

internal sealed class HistoryDotNetFactMaterialization(
    IReadOnlyDictionary<string, IReadOnlyList<HistoryDotNetTypeContext>> typesByCanonicalPath)
{
    public IReadOnlyDictionary<string, IReadOnlyList<HistoryDotNetTypeContext>> TypesByCanonicalPath { get; } =
        typesByCanonicalPath;
}
