namespace ArchLinterNet.Core.History;

internal sealed class HistoryIngestionRequest(string repositoryPath, string authoredFrom, string authoredTo)
{
    public string RepositoryPath { get; } = repositoryPath;

    // Exclusive.
    public string AuthoredFrom { get; } = authoredFrom;

    // Inclusive.
    public string AuthoredTo { get; } = authoredTo;
}
