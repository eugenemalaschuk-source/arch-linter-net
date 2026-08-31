namespace ArchLinterNet.Core.Model;

/// <summary>Gate decision for a complete architecture-health projection.</summary>
public enum ArchitectureHealthGate
{
    Pass,
    Fail,
    Unassessable,
}

/// <summary>Non-compensating health classification for architecture governance.</summary>
public enum ArchitectureHealthState
{
    Healthy,
    Debt,
    Degrading,
    Failing,
    Unassessable,
}

/// <summary>Typed state of one independently owned health dimension.</summary>
public enum ArchitectureHealthDimensionState
{
    Pass,
    Fail,
    Debt,
    Degrading,
    Unassessable,
    NotConfigured,
    NotApplicable,
}

/// <summary>Stable machine-readable reason retained by a health dimension.</summary>
public sealed record ArchitectureHealthReason(string Code, string Source)
{
    public ArchitectureHealthReason(string code)
        : this(code, string.Empty)
    {
    }
}

/// <summary>One deterministic projection of a separately authoritative governance dimension.</summary>
public sealed record ArchitectureHealthDimension(
    string Name,
    ArchitectureHealthDimensionState State,
    IReadOnlyList<ArchitectureHealthReason> Reasons)
{
    public IReadOnlyList<ArchitectureHealthReason> Reasons { get; init; } = (Reasons ?? throw new ArgumentNullException(nameof(Reasons)))
        .OrderBy(reason => reason.Code, StringComparer.Ordinal)
        .ThenBy(reason => reason.Source, StringComparer.Ordinal)
        .ToArray();
}

/// <summary>Versioned, deterministic repository-level architecture-health summary.</summary>
public sealed record ArchitectureHealthSummary(
    string SchemaId,
    ArchitectureHealthGate Gate,
    ArchitectureHealthState Health,
    IReadOnlyList<ArchitectureHealthDimension> Dimensions)
{
    /// <summary>Current machine-readable architecture-health contract identifier.</summary>
    public const string CurrentSchemaId = "architecture-health/v1";

    public IReadOnlyList<ArchitectureHealthDimension> Dimensions { get; init; } =
        (Dimensions ?? throw new ArgumentNullException(nameof(Dimensions)))
        .OrderBy(dimension => dimension.Name, StringComparer.Ordinal)
        .ToArray();
}
