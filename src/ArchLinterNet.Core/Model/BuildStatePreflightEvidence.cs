namespace ArchLinterNet.Core.Model;

public sealed record BuildStatePreflightEvidence(
    string ProjectPath,
    string AssemblyName,
    string? RequestedConfiguration = null,
    string? ObservedConfiguration = null,
    string? RequestedTargetFramework = null,
    string? ObservedTargetFramework = null,
    string? ExpectedOutputPath = null,
    IReadOnlyCollection<string>? SearchedPaths = null,
    string? BuildCommand = null,
    string? Detail = null)
{
    public IReadOnlyCollection<string> SearchedPaths { get; init; } = SearchedPaths ?? Array.Empty<string>();
}
