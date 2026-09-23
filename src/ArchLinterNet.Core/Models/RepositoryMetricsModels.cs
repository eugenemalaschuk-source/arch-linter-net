using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Model;

public enum RepositoryMetricsAvailability
{
    Complete,
    Partial,
    Unavailable,
}

public static class RepositoryMetricsReasonCodes
{
    public const string MissingAnalysis = "missing_analysis";
    public const string MissingProjectDiscovery = "missing_project_discovery";
    public const string IncompleteTypeUniverse = "incomplete_type_universe";
    public const string SourceInventoryNotMaterialized = "source_inventory_not_materialized";
    public const string UnreadableSource = "unreadable_source";
    public const string DiscoveryDiagnostics = "project_discovery_diagnostics";
    public const string IncompatibleSchema = "incompatible_schema";
}

public sealed record RepositoryMetricsSnapshot(
    int SchemaVersion,
    string Kind,
    RepositoryMetricsAvailability Availability,
    IReadOnlyList<string> ReasonCodes,
    RepositorySizeMetrics Size,
    RepositoryCouplingMetrics Coupling,
    RepositoryStructureMetrics Structure)
{
    public const int CurrentSchemaVersion = 1;
    public const string CurrentKind = "repository-metrics/v1";

    public IReadOnlyList<string> ReasonCodes { get; init; } = (ReasonCodes ?? Array.Empty<string>())
        .Where(static reason => !string.IsNullOrWhiteSpace(reason))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static reason => reason, StringComparer.Ordinal)
        .ToArray();

    [JsonIgnore]
    public bool IsCompatible => SchemaVersion == CurrentSchemaVersion
        && string.Equals(Kind, CurrentKind, StringComparison.Ordinal);

    [JsonIgnore]
    public bool IsComplete => Availability == RepositoryMetricsAvailability.Complete && IsCompatible;

    public static RepositoryMetricsSnapshot Unavailable(params string[] reasons) => new(
        CurrentSchemaVersion,
        CurrentKind,
        RepositoryMetricsAvailability.Unavailable,
        reasons,
        RepositorySizeMetrics.Empty,
        RepositoryCouplingMetrics.Empty,
        RepositoryStructureMetrics.Empty);
}

public sealed record RepositorySizeMetrics(
    int? SourceLines,
    int? SourceFiles,
    int? Projects,
    int? Types,
    int? PublicTypes)
{
    public static RepositorySizeMetrics Empty { get; } = new(null, null, null, null, null);
}

public sealed record RepositoryCouplingMetrics(
    int? DependencyCount,
    double? DependenciesPerProject,
    double? DependencyDensity,
    int? MaxFanIn,
    int? MaxFanOut,
    IReadOnlyList<RepositoryProjectCoupling> Projects)
{
    public IReadOnlyList<RepositoryProjectCoupling> Projects { get; init; } = (Projects ?? Array.Empty<RepositoryProjectCoupling>())
        .OrderBy(static project => project.Identity, StringComparer.Ordinal)
        .ToArray();

    public static RepositoryCouplingMetrics Empty { get; } = new(null, null, null, null, null, Array.Empty<RepositoryProjectCoupling>());
}

public sealed record RepositoryProjectCoupling(
    string Identity,
    string Name,
    int FanIn,
    int FanOut,
    int AfferentCoupling,
    int EfferentCoupling,
    double Instability);

public sealed record RepositoryStructureMetrics(
    int? MaxDependencyDepth,
    int? CyclicComponentCount,
    int? CyclicProjectCount,
    double? CyclicProjectRatio,
    int? LargestSccSize,
    double? LargestSccRatio)
{
    public static RepositoryStructureMetrics Empty { get; } = new(null, null, null, null, null, null);
}

public enum RepositoryMetricsDeltaAvailability
{
    Complete,
    Unavailable,
}

public sealed record RepositoryMetricDelta(
    string Name,
    string Unit,
    double Base,
    double Head,
    double Delta);

public sealed record RepositoryMetricsDelta(
    int SchemaVersion,
    string Kind,
    RepositoryMetricsDeltaAvailability Availability,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<RepositoryMetricDelta> Metrics)
{
    public const int CurrentSchemaVersion = 1;
    public const string CurrentKind = "repository-metrics-delta/v1";

    public IReadOnlyList<string> ReasonCodes { get; init; } = (ReasonCodes ?? Array.Empty<string>())
        .Where(static reason => !string.IsNullOrWhiteSpace(reason))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static reason => reason, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<RepositoryMetricDelta> Metrics { get; init; } = (Metrics ?? Array.Empty<RepositoryMetricDelta>())
        .OrderBy(static metric => metric.Name, StringComparer.Ordinal)
        .ToArray();

    [JsonIgnore]
    public bool IsCompatible => SchemaVersion == CurrentSchemaVersion
        && string.Equals(Kind, CurrentKind, StringComparison.Ordinal);

    [JsonIgnore]
    public bool IsComplete => Availability == RepositoryMetricsDeltaAvailability.Complete && IsCompatible;

    public static RepositoryMetricsDelta Unavailable(params string[] reasons) => new(
        CurrentSchemaVersion,
        CurrentKind,
        RepositoryMetricsDeltaAvailability.Unavailable,
        reasons,
        Array.Empty<RepositoryMetricDelta>());
}

public static class RepositoryMetricsDeltaFactory
{
    public static RepositoryMetricsDelta Create(
        RepositoryMetricsSnapshot? baseline,
        RepositoryMetricsSnapshot? current)
    {
        if (baseline is null || current is null)
        {
            return RepositoryMetricsDelta.Unavailable("missing_base_or_head_metrics");
        }

        if (!baseline.IsCompatible || !current.IsCompatible)
        {
            return RepositoryMetricsDelta.Unavailable(RepositoryMetricsReasonCodes.IncompatibleSchema);
        }

        if (!baseline.IsComplete || !current.IsComplete)
        {
            string[] reasons = baseline.ReasonCodes
                .Concat(current.ReasonCodes)
                .Append("incomplete_base_or_head_metrics")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return RepositoryMetricsDelta.Unavailable(reasons);
        }

        List<RepositoryMetricDelta> metrics = [];
        Add(metrics, "source_lines", "count", baseline.Size.SourceLines, current.Size.SourceLines);
        Add(metrics, "source_files", "count", baseline.Size.SourceFiles, current.Size.SourceFiles);
        Add(metrics, "projects", "count", baseline.Size.Projects, current.Size.Projects);
        Add(metrics, "types", "count", baseline.Size.Types, current.Size.Types);
        Add(metrics, "public_types", "count", baseline.Size.PublicTypes, current.Size.PublicTypes);
        Add(metrics, "dependency_count", "count", baseline.Coupling.DependencyCount, current.Coupling.DependencyCount);
        Add(metrics, "dependencies_per_project", "ratio", baseline.Coupling.DependenciesPerProject, current.Coupling.DependenciesPerProject);
        Add(metrics, "dependency_density", "ratio", baseline.Coupling.DependencyDensity, current.Coupling.DependencyDensity);
        Add(metrics, "max_fan_in", "count", baseline.Coupling.MaxFanIn, current.Coupling.MaxFanIn);
        Add(metrics, "max_fan_out", "count", baseline.Coupling.MaxFanOut, current.Coupling.MaxFanOut);
        Add(metrics, "max_dependency_depth", "count", baseline.Structure.MaxDependencyDepth, current.Structure.MaxDependencyDepth);
        Add(metrics, "cyclic_component_count", "count", baseline.Structure.CyclicComponentCount, current.Structure.CyclicComponentCount);
        Add(metrics, "cyclic_project_count", "count", baseline.Structure.CyclicProjectCount, current.Structure.CyclicProjectCount);
        Add(metrics, "cyclic_project_ratio", "ratio", baseline.Structure.CyclicProjectRatio, current.Structure.CyclicProjectRatio);
        Add(metrics, "largest_scc_size", "count", baseline.Structure.LargestSccSize, current.Structure.LargestSccSize);
        Add(metrics, "largest_scc_ratio", "ratio", baseline.Structure.LargestSccRatio, current.Structure.LargestSccRatio);
        return new(
            RepositoryMetricsDelta.CurrentSchemaVersion,
            RepositoryMetricsDelta.CurrentKind,
            RepositoryMetricsDeltaAvailability.Complete,
            Array.Empty<string>(),
            metrics);
    }

    private static void Add(
        List<RepositoryMetricDelta> metrics,
        string name,
        string unit,
        double? baseline,
        double? current)
    {
        if (baseline is null || current is null)
        {
            return;
        }

        metrics.Add(new RepositoryMetricDelta(name, unit, baseline.Value, current.Value, current.Value - baseline.Value));
    }
}

public static class RepositoryMetricsJson
{
    private static readonly JsonSerializerOptions _options = new()
    {
        AllowTrailingCommas = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.Strict,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        WriteIndented = true,
    };

    static RepositoryMetricsJson()
    {
        _options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    }

    public static string Serialize(RepositoryMetricsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.IsCompatible)
        {
            throw new ArgumentException("Repository metrics use an unsupported schema.", nameof(snapshot));
        }

        return JsonSerializer.Serialize(snapshot, _options);
    }

    public static string Serialize(RepositoryMetricsDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        if (!delta.IsCompatible)
        {
            throw new ArgumentException("Repository metric deltas use an unsupported schema.", nameof(delta));
        }

        return JsonSerializer.Serialize(delta, _options);
    }

    public static RepositoryMetricsSnapshot Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        RepositoryMetricsSnapshot snapshot = JsonSerializer.Deserialize<RepositoryMetricsSnapshot>(json, _options)
            ?? throw new ArgumentException("The repository metrics artifact is empty.", nameof(json));
        if (!snapshot.IsCompatible)
        {
            throw new ArgumentException("The repository metrics artifact uses an unsupported schema.", nameof(json));
        }

        return snapshot;
    }

    public static RepositoryMetricsDelta DeserializeDelta(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        RepositoryMetricsDelta delta = JsonSerializer.Deserialize<RepositoryMetricsDelta>(json, _options)
            ?? throw new ArgumentException("The repository metrics delta artifact is empty.", nameof(json));
        if (!delta.IsCompatible)
        {
            throw new ArgumentException("The repository metrics delta artifact uses an unsupported schema.", nameof(json));
        }

        return delta;
    }

    internal static JsonSerializerOptions Options => _options;
}
