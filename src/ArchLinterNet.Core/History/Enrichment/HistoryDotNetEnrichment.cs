using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.History.Enrichment;

internal sealed class HistoryDotNetEnrichment(
    HistoryDotNetEnrichmentStatus status,
    string? reason,
    IReadOnlyList<HistoryDotNetFileEnrichment> files)
{
    public HistoryDotNetEnrichmentStatus Status { get; } = status;

    public string? Reason { get; } = reason;

    public IReadOnlyList<HistoryDotNetFileEnrichment> Files { get; } = files;

    public static HistoryDotNetEnrichment NotRequested { get; } = new(
        HistoryDotNetEnrichmentStatus.NotRequested, null, Array.Empty<HistoryDotNetFileEnrichment>());

    public static HistoryDotNetEnrichment Unavailable(string reason) => new(
        HistoryDotNetEnrichmentStatus.Unavailable, reason, Array.Empty<HistoryDotNetFileEnrichment>());
}

internal enum HistoryDotNetEnrichmentStatus
{
    NotRequested,
    NotApplicable,
    Available,
    Unavailable
}

internal sealed class HistoryDotNetFileEnrichment(
    string canonicalPath,
    HistoryDotNetFileEnrichmentStatus status,
    IReadOnlyList<HistoryDotNetTypeContext> types)
{
    public string CanonicalPath { get; } = canonicalPath;

    public HistoryDotNetFileEnrichmentStatus Status { get; } = status;

    public IReadOnlyList<HistoryDotNetTypeContext> Types { get; } = types;
}

internal enum HistoryDotNetFileEnrichmentStatus
{
    Available,
    NotApplicable
}

internal sealed class HistoryDotNetTypeContext(
    string projectPath,
    string assemblyName,
    string namespaceName,
    string fullTypeName,
    string simpleTypeName,
    ArchitectureTypeKind typeKind,
    bool isAbstract)
{
    public string ProjectPath { get; } = projectPath;

    public string AssemblyName { get; } = assemblyName;

    public string NamespaceName { get; } = namespaceName;

    public string FullTypeName { get; } = fullTypeName;

    public string SimpleTypeName { get; } = simpleTypeName;

    public ArchitectureTypeKind TypeKind { get; } = typeKind;

    public bool IsAbstract { get; } = isAbstract;
}
