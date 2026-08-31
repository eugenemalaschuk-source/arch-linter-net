namespace ArchLinterNet.Core.Model;

/// <summary>
/// Canonical effective-policy inventory attached to one analyzed validation outcome.
/// </summary>
/// <remarks>
/// This inventory describes configured controls and explicit manual-waiver debt. It is deliberately
/// independent of findings, baseline debt, and the number of subjects a control happened to inspect.
/// </remarks>
public sealed record ArchitecturePolicyInventory(
    string SchemaId,
    int EffectiveRuleCount,
    ArchitecturePolicyInventoryRules Rules,
    ArchitecturePolicyInventoryIgnoreDebt IgnoreDebt,
    IReadOnlyList<ArchitectureWaiverLifecycleRecord> Waivers)
{
    /// <summary>The current versioned inventory identifier.</summary>
    public const string CurrentSchemaId = "architecture-policy-inventory/v1";
}

/// <summary>
/// Disjoint partition of effective controls: non-coverage strict, non-coverage audit, and coverage.
/// </summary>
public sealed record ArchitecturePolicyInventoryRules(
    int Strict,
    int Audit,
    int Coverage);

/// <summary>Summary of explicit manual-waiver lifecycle debt.</summary>
public sealed record ArchitecturePolicyInventoryIgnoreDebt(
    int Total,
    int Active,
    int Stale,
    int Expired,
    int MetadataIncomplete,
    int Invalid);
