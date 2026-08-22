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

    public HistoryEnrichmentProjection ToReportProjection(string resolvedTo)
    {
        HistoryEnrichmentStatus projectionStatus = Status switch
        {
            HistoryDotNetEnrichmentStatus.NotRequested => HistoryEnrichmentStatus.NotRequested,
            HistoryDotNetEnrichmentStatus.NotApplicable => HistoryEnrichmentStatus.NotApplicable,
            HistoryDotNetEnrichmentStatus.Available => HistoryEnrichmentStatus.Available,
            _ => HistoryEnrichmentStatus.Unavailable,
        };
        if (projectionStatus == HistoryEnrichmentStatus.NotRequested)
        {
            return HistoryEnrichmentProjection.NotRequested;
        }

        List<HistoryEnrichmentProvenance> provenance = [new("dotnet.revision", resolvedTo)];
        if (projectionStatus is HistoryEnrichmentStatus.Available or HistoryEnrichmentStatus.NotApplicable)
        {
            provenance.Add(new HistoryEnrichmentProvenance("dotnet.provider", "core-project-source-facts"));
        }

        List<HistoryEnrichmentContext> context = [];
        HashSet<(string Kind, string Value)> seen = [];
        void AddContext(string kind, string value)
        {
            if (seen.Add((kind, value)))
            {
                context.Add(new HistoryEnrichmentContext(kind, value));
            }
        }

        foreach (HistoryDotNetFileEnrichment file in Files.OrderBy(file => file.CanonicalPath, StringComparer.Ordinal))
        {
            AddContext(
                file.Status == HistoryDotNetFileEnrichmentStatus.Available
                    ? "dotnet.file.available"
                    : "dotnet.file.not_applicable",
                file.CanonicalPath);
            foreach (HistoryDotNetTypeContext type in file.Types
                         .OrderBy(type => type.ProjectPath, StringComparer.Ordinal)
                         .ThenBy(type => type.AssemblyName, StringComparer.Ordinal)
                         .ThenBy(type => type.FullTypeName, StringComparer.Ordinal))
            {
                string subject = $"{file.CanonicalPath} :: {type.FullTypeName}";
                AddContext("dotnet.type", subject);
                AddContext("dotnet.project", $"{subject} :: {type.ProjectPath}");
                AddContext("dotnet.assembly", $"{subject} :: {type.AssemblyName}");
                AddContext("dotnet.namespace", $"{subject} :: {type.NamespaceName}");
                AddContext("dotnet.type_kind", $"{subject} :: {type.TypeKind.ToString().ToLowerInvariant()}");
                AddContext("dotnet.is_abstract", $"{subject} :: {(type.IsAbstract ? "true" : "false")}");
            }
        }

        return new HistoryEnrichmentProjection(projectionStatus, Reason, provenance, context);
    }
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
