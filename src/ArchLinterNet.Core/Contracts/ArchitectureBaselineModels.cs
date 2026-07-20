using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

public sealed record ArchitectureBaselineCandidate(
    string ContractGroup,
    string? ContractId,
    string SourceType,
    string ForbiddenReference,
    ArchitectureViolationIdentity? Identity = null);

/// <summary>
/// Assigns a stable, non-line-based <see cref="ArchitectureViolationIdentity.Occurrence"/> discriminator
/// to candidates that otherwise share every other identity field, so distinct occurrences of the same
/// source/target pair within one contract never collapse into a single baseline entry. Runs once, in the
/// deterministic order candidates were collected in, immediately after collection — before both baseline
/// comparison and baseline generation consume the list, so the two stay consistent with each other.
/// </summary>
public static class ArchitectureBaselineCandidateOccurrenceAssigner
{
    public static IReadOnlyList<ArchitectureBaselineCandidate> Assign(IReadOnlyList<ArchitectureBaselineCandidate> candidates)
    {
        var counters = new Dictionary<(string ContractGroup, string? ContractId, ArchitectureViolationIdentity Identity), int>();
        var result = new List<ArchitectureBaselineCandidate>(candidates.Count);

        foreach (ArchitectureBaselineCandidate candidate in candidates)
        {
            if (candidate.Identity == null)
            {
                result.Add(candidate);
                continue;
            }

            var key = (candidate.ContractGroup, candidate.ContractId, candidate.Identity with { Occurrence = 0 });
            int occurrence = counters.TryGetValue(key, out int count) ? count : 0;
            counters[key] = occurrence + 1;

            result.Add(candidate with { Identity = candidate.Identity with { Occurrence = occurrence } });
        }

        return result;
    }
}

public sealed class ArchitectureBaselineDocument
{
    [YamlMember(Alias = "version")]
    public int Version { get; set; }

    [YamlMember(Alias = "baseline")]
    public ArchitectureBaselineContractGroups Baseline { get; set; } = new();
}

// Property order below matches ArchitectureContractCatalog.Build's baseline-capable family order
// (every executable group except asmdef and layer_templates, which never produce baseline
// candidates). GroupNames, GetGroup, and SetGroup are the single source of truth the baseline
// generator and comparer read from, so a new baseline-capable group is added in exactly one place.
public sealed class ArchitectureBaselineContractGroups
{
    [YamlMember(Alias = "strict")] public List<ArchitectureBaselineContractEntry> Strict { get; set; } = new();
    [YamlMember(Alias = "audit")] public List<ArchitectureBaselineContractEntry> Audit { get; set; } = new();
    [YamlMember(Alias = "strict_layers")] public List<ArchitectureBaselineContractEntry> StrictLayers { get; set; } = new();
    [YamlMember(Alias = "audit_layers")] public List<ArchitectureBaselineContractEntry> AuditLayers { get; set; } = new();
    [YamlMember(Alias = "strict_allow_only")] public List<ArchitectureBaselineContractEntry> StrictAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_allow_only")] public List<ArchitectureBaselineContractEntry> AuditAllowOnly { get; set; } = new();
    [YamlMember(Alias = "strict_cycles")] public List<ArchitectureBaselineContractEntry> StrictCycles { get; set; } = new();
    [YamlMember(Alias = "audit_cycles")] public List<ArchitectureBaselineContractEntry> AuditCycles { get; set; } = new();
    [YamlMember(Alias = "strict_method_body")] public List<ArchitectureBaselineContractEntry> StrictMethodBody { get; set; } = new();
    [YamlMember(Alias = "audit_method_body")] public List<ArchitectureBaselineContractEntry> AuditMethodBody { get; set; } = new();
    [YamlMember(Alias = "strict_independence")] public List<ArchitectureBaselineContractEntry> StrictIndependence { get; set; } = new();
    [YamlMember(Alias = "audit_independence")] public List<ArchitectureBaselineContractEntry> AuditIndependence { get; set; } = new();
    [YamlMember(Alias = "strict_assembly_independence")] public List<ArchitectureBaselineContractEntry> StrictAssemblyIndependence { get; set; } = new();
    [YamlMember(Alias = "audit_assembly_independence")] public List<ArchitectureBaselineContractEntry> AuditAssemblyIndependence { get; set; } = new();
    [YamlMember(Alias = "strict_assembly_dependency")] public List<ArchitectureBaselineContractEntry> StrictAssemblyDependency { get; set; } = new();
    [YamlMember(Alias = "audit_assembly_dependency")] public List<ArchitectureBaselineContractEntry> AuditAssemblyDependency { get; set; } = new();
    [YamlMember(Alias = "strict_assembly_allow_only")] public List<ArchitectureBaselineContractEntry> StrictAssemblyAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_assembly_allow_only")] public List<ArchitectureBaselineContractEntry> AuditAssemblyAllowOnly { get; set; } = new();
    [YamlMember(Alias = "strict_package_dependency")] public List<ArchitectureBaselineContractEntry> StrictPackageDependency { get; set; } = new();
    [YamlMember(Alias = "audit_package_dependency")] public List<ArchitectureBaselineContractEntry> AuditPackageDependency { get; set; } = new();
    [YamlMember(Alias = "strict_package_allow_only")] public List<ArchitectureBaselineContractEntry> StrictPackageAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_package_allow_only")] public List<ArchitectureBaselineContractEntry> AuditPackageAllowOnly { get; set; } = new();
    [YamlMember(Alias = "strict_project_metadata")] public List<ArchitectureBaselineContractEntry> StrictProjectMetadata { get; set; } = new();
    [YamlMember(Alias = "audit_project_metadata")] public List<ArchitectureBaselineContractEntry> AuditProjectMetadata { get; set; } = new();
    [YamlMember(Alias = "strict_protected")] public List<ArchitectureBaselineContractEntry> StrictProtected { get; set; } = new();
    [YamlMember(Alias = "audit_protected")] public List<ArchitectureBaselineContractEntry> AuditProtected { get; set; } = new();
    [YamlMember(Alias = "strict_external")] public List<ArchitectureBaselineContractEntry> StrictExternal { get; set; } = new();
    [YamlMember(Alias = "audit_external")] public List<ArchitectureBaselineContractEntry> AuditExternal { get; set; } = new();
    [YamlMember(Alias = "strict_external_allow_only")] public List<ArchitectureBaselineContractEntry> StrictExternalAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_external_allow_only")] public List<ArchitectureBaselineContractEntry> AuditExternalAllowOnly { get; set; } = new();
    [YamlMember(Alias = "strict_acyclic_siblings")] public List<ArchitectureBaselineContractEntry> StrictAcyclicSiblings { get; set; } = new();
    [YamlMember(Alias = "audit_acyclic_siblings")] public List<ArchitectureBaselineContractEntry> AuditAcyclicSiblings { get; set; } = new();
    [YamlMember(Alias = "strict_type_placement")] public List<ArchitectureBaselineContractEntry> StrictTypePlacement { get; set; } = new();
    [YamlMember(Alias = "audit_type_placement")] public List<ArchitectureBaselineContractEntry> AuditTypePlacement { get; set; } = new();
    [YamlMember(Alias = "strict_layout_conventions")] public List<ArchitectureBaselineContractEntry> StrictLayoutConventions { get; set; } = new();
    [YamlMember(Alias = "audit_layout_conventions")] public List<ArchitectureBaselineContractEntry> AuditLayoutConventions { get; set; } = new();
    [YamlMember(Alias = "strict_public_api_surface")] public List<ArchitectureBaselineContractEntry> StrictPublicApiSurface { get; set; } = new();
    [YamlMember(Alias = "audit_public_api_surface")] public List<ArchitectureBaselineContractEntry> AuditPublicApiSurface { get; set; } = new();
    [YamlMember(Alias = "strict_attribute_usage")] public List<ArchitectureBaselineContractEntry> StrictAttributeUsage { get; set; } = new();
    [YamlMember(Alias = "audit_attribute_usage")] public List<ArchitectureBaselineContractEntry> AuditAttributeUsage { get; set; } = new();
    [YamlMember(Alias = "strict_inheritance")] public List<ArchitectureBaselineContractEntry> StrictInheritance { get; set; } = new();
    [YamlMember(Alias = "audit_inheritance")] public List<ArchitectureBaselineContractEntry> AuditInheritance { get; set; } = new();
    [YamlMember(Alias = "strict_interface_implementation")] public List<ArchitectureBaselineContractEntry> StrictInterfaceImplementation { get; set; } = new();
    [YamlMember(Alias = "audit_interface_implementation")] public List<ArchitectureBaselineContractEntry> AuditInterfaceImplementation { get; set; } = new();
    [YamlMember(Alias = "strict_composition")] public List<ArchitectureBaselineContractEntry> StrictComposition { get; set; } = new();
    [YamlMember(Alias = "audit_composition")] public List<ArchitectureBaselineContractEntry> AuditComposition { get; set; } = new();
    [YamlMember(Alias = "strict_coverage")] public List<ArchitectureBaselineContractEntry> StrictCoverage { get; set; } = new();
    [YamlMember(Alias = "audit_coverage")] public List<ArchitectureBaselineContractEntry> AuditCoverage { get; set; } = new();
    [YamlMember(Alias = "strict_context_dependencies")] public List<ArchitectureBaselineContractEntry> StrictContextDependencies { get; set; } = new();
    [YamlMember(Alias = "audit_context_dependencies")] public List<ArchitectureBaselineContractEntry> AuditContextDependencies { get; set; } = new();
    [YamlMember(Alias = "strict_context_allow_only")] public List<ArchitectureBaselineContractEntry> StrictContextAllowOnly { get; set; } = new();
    [YamlMember(Alias = "audit_context_allow_only")] public List<ArchitectureBaselineContractEntry> AuditContextAllowOnly { get; set; } = new();
    [YamlMember(Alias = "strict_port_boundaries")] public List<ArchitectureBaselineContractEntry> StrictPortBoundaries { get; set; } = new();
    [YamlMember(Alias = "audit_port_boundaries")] public List<ArchitectureBaselineContractEntry> AuditPortBoundaries { get; set; } = new();

    // Canonical, ordered set of every baseline-capable group name. The baseline comparer iterates
    // this list; keeping it aligned with the properties above (and with the catalog's resolvable
    // groups, asserted by ArchitectureBaselineGroupCoverageTests) prevents silent drift where an
    // executable group would be classified/serialized by one component but ignored by another.
    public static readonly IReadOnlyList<string> GroupNames = new[]
    {
        "strict", "audit",
        "strict_layers", "audit_layers",
        "strict_allow_only", "audit_allow_only",
        "strict_cycles", "audit_cycles",
        "strict_method_body", "audit_method_body",
        "strict_independence", "audit_independence",
        "strict_assembly_independence", "audit_assembly_independence",
        "strict_assembly_dependency", "audit_assembly_dependency",
        "strict_assembly_allow_only", "audit_assembly_allow_only",
        "strict_package_dependency", "audit_package_dependency",
        "strict_package_allow_only", "audit_package_allow_only",
        "strict_project_metadata", "audit_project_metadata",
        "strict_protected", "audit_protected",
        "strict_external", "audit_external",
        "strict_external_allow_only", "audit_external_allow_only",
        "strict_acyclic_siblings", "audit_acyclic_siblings",
        "strict_type_placement", "audit_type_placement",
        "strict_layout_conventions", "audit_layout_conventions",
        "strict_public_api_surface", "audit_public_api_surface",
        "strict_attribute_usage", "audit_attribute_usage",
        "strict_inheritance", "audit_inheritance",
        "strict_interface_implementation", "audit_interface_implementation",
        "strict_composition", "audit_composition",
        "strict_coverage", "audit_coverage",
        "strict_context_dependencies", "audit_context_dependencies",
        "strict_context_allow_only", "audit_context_allow_only",
        "strict_port_boundaries", "audit_port_boundaries",
    };

    public List<ArchitectureBaselineContractEntry> GetGroup(string groupName)
    {
        return groupName switch
        {
            "strict" => Strict,
            "audit" => Audit,
            "strict_layers" => StrictLayers,
            "audit_layers" => AuditLayers,
            "strict_allow_only" => StrictAllowOnly,
            "audit_allow_only" => AuditAllowOnly,
            "strict_cycles" => StrictCycles,
            "audit_cycles" => AuditCycles,
            "strict_method_body" => StrictMethodBody,
            "audit_method_body" => AuditMethodBody,
            "strict_independence" => StrictIndependence,
            "audit_independence" => AuditIndependence,
            "strict_assembly_independence" => StrictAssemblyIndependence,
            "audit_assembly_independence" => AuditAssemblyIndependence,
            "strict_assembly_dependency" => StrictAssemblyDependency,
            "audit_assembly_dependency" => AuditAssemblyDependency,
            "strict_assembly_allow_only" => StrictAssemblyAllowOnly,
            "audit_assembly_allow_only" => AuditAssemblyAllowOnly,
            "strict_package_dependency" => StrictPackageDependency,
            "audit_package_dependency" => AuditPackageDependency,
            "strict_package_allow_only" => StrictPackageAllowOnly,
            "audit_package_allow_only" => AuditPackageAllowOnly,
            "strict_project_metadata" => StrictProjectMetadata,
            "audit_project_metadata" => AuditProjectMetadata,
            "strict_protected" => StrictProtected,
            "audit_protected" => AuditProtected,
            "strict_external" => StrictExternal,
            "audit_external" => AuditExternal,
            "strict_external_allow_only" => StrictExternalAllowOnly,
            "audit_external_allow_only" => AuditExternalAllowOnly,
            "strict_acyclic_siblings" => StrictAcyclicSiblings,
            "audit_acyclic_siblings" => AuditAcyclicSiblings,
            "strict_type_placement" => StrictTypePlacement,
            "audit_type_placement" => AuditTypePlacement,
            "strict_layout_conventions" => StrictLayoutConventions,
            "audit_layout_conventions" => AuditLayoutConventions,
            "strict_public_api_surface" => StrictPublicApiSurface,
            "audit_public_api_surface" => AuditPublicApiSurface,
            "strict_attribute_usage" => StrictAttributeUsage,
            "audit_attribute_usage" => AuditAttributeUsage,
            "strict_inheritance" => StrictInheritance,
            "audit_inheritance" => AuditInheritance,
            "strict_interface_implementation" => StrictInterfaceImplementation,
            "audit_interface_implementation" => AuditInterfaceImplementation,
            "strict_composition" => StrictComposition,
            "audit_composition" => AuditComposition,
            "strict_coverage" => StrictCoverage,
            "audit_coverage" => AuditCoverage,
            "strict_context_dependencies" => StrictContextDependencies,
            "audit_context_dependencies" => AuditContextDependencies,
            "strict_context_allow_only" => StrictContextAllowOnly,
            "audit_context_allow_only" => AuditContextAllowOnly,
            "strict_port_boundaries" => StrictPortBoundaries,
            "audit_port_boundaries" => AuditPortBoundaries,
            _ => throw new ArgumentOutOfRangeException(
                nameof(groupName), groupName, "Unknown baseline group. Add it to ArchitectureBaselineContractGroups."),
        };
    }

    public void SetGroup(string groupName, List<ArchitectureBaselineContractEntry> entries)
    {
        switch (groupName)
        {
            case "strict": Strict = entries; break;
            case "audit": Audit = entries; break;
            case "strict_layers": StrictLayers = entries; break;
            case "audit_layers": AuditLayers = entries; break;
            case "strict_allow_only": StrictAllowOnly = entries; break;
            case "audit_allow_only": AuditAllowOnly = entries; break;
            case "strict_cycles": StrictCycles = entries; break;
            case "audit_cycles": AuditCycles = entries; break;
            case "strict_method_body": StrictMethodBody = entries; break;
            case "audit_method_body": AuditMethodBody = entries; break;
            case "strict_independence": StrictIndependence = entries; break;
            case "audit_independence": AuditIndependence = entries; break;
            case "strict_assembly_independence": StrictAssemblyIndependence = entries; break;
            case "audit_assembly_independence": AuditAssemblyIndependence = entries; break;
            case "strict_assembly_dependency": StrictAssemblyDependency = entries; break;
            case "audit_assembly_dependency": AuditAssemblyDependency = entries; break;
            case "strict_assembly_allow_only": StrictAssemblyAllowOnly = entries; break;
            case "audit_assembly_allow_only": AuditAssemblyAllowOnly = entries; break;
            case "strict_package_dependency": StrictPackageDependency = entries; break;
            case "audit_package_dependency": AuditPackageDependency = entries; break;
            case "strict_package_allow_only": StrictPackageAllowOnly = entries; break;
            case "audit_package_allow_only": AuditPackageAllowOnly = entries; break;
            case "strict_project_metadata": StrictProjectMetadata = entries; break;
            case "audit_project_metadata": AuditProjectMetadata = entries; break;
            case "strict_protected": StrictProtected = entries; break;
            case "audit_protected": AuditProtected = entries; break;
            case "strict_external": StrictExternal = entries; break;
            case "audit_external": AuditExternal = entries; break;
            case "strict_external_allow_only": StrictExternalAllowOnly = entries; break;
            case "audit_external_allow_only": AuditExternalAllowOnly = entries; break;
            case "strict_acyclic_siblings": StrictAcyclicSiblings = entries; break;
            case "audit_acyclic_siblings": AuditAcyclicSiblings = entries; break;
            case "strict_type_placement": StrictTypePlacement = entries; break;
            case "audit_type_placement": AuditTypePlacement = entries; break;
            case "strict_layout_conventions": StrictLayoutConventions = entries; break;
            case "audit_layout_conventions": AuditLayoutConventions = entries; break;
            case "strict_public_api_surface": StrictPublicApiSurface = entries; break;
            case "audit_public_api_surface": AuditPublicApiSurface = entries; break;
            case "strict_attribute_usage": StrictAttributeUsage = entries; break;
            case "audit_attribute_usage": AuditAttributeUsage = entries; break;
            case "strict_inheritance": StrictInheritance = entries; break;
            case "audit_inheritance": AuditInheritance = entries; break;
            case "strict_interface_implementation": StrictInterfaceImplementation = entries; break;
            case "audit_interface_implementation": AuditInterfaceImplementation = entries; break;
            case "strict_composition": StrictComposition = entries; break;
            case "audit_composition": AuditComposition = entries; break;
            case "strict_coverage": StrictCoverage = entries; break;
            case "audit_coverage": AuditCoverage = entries; break;
            case "strict_context_dependencies": StrictContextDependencies = entries; break;
            case "audit_context_dependencies": AuditContextDependencies = entries; break;
            case "strict_context_allow_only": StrictContextAllowOnly = entries; break;
            case "audit_context_allow_only": AuditContextAllowOnly = entries; break;
            case "strict_port_boundaries": StrictPortBoundaries = entries; break;
            case "audit_port_boundaries": AuditPortBoundaries = entries; break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(groupName), groupName, "Unknown baseline group. Add it to ArchitectureBaselineContractGroups.");
        }
    }
}

public sealed class ArchitectureBaselineContractEntry
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureBaselineIgnoredViolation> IgnoredViolations { get; set; } = new();
}

/// <summary>
/// One <c>ignored_violations</c> entry inside a baseline file. Version-1 files only ever populate
/// <see cref="SourceType"/>/<see cref="ForbiddenReference"/>/<see cref="Reason"/> (the legacy shape);
/// version-2 files additionally populate the structured identity fields below, which is what
/// <see cref="ArchitectureBaselineComparer"/> and <see cref="ArchitectureBaselineGenerator"/> use for
/// matching once the containing document's <see cref="ArchitectureBaselineDocument.Version"/> is 2.
/// </summary>
public sealed class ArchitectureBaselineIgnoredViolation
{
    [YamlMember(Alias = "source_type")] public string SourceType { get; set; } = string.Empty;

    [YamlMember(Alias = "forbidden_reference")]
    public string ForbiddenReference { get; set; } = string.Empty;

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;

    [YamlMember(Alias = "identity_version")] public int? IdentityVersion { get; set; }

    [YamlMember(Alias = "contract_family")] public string? ContractFamily { get; set; }

    [YamlMember(Alias = "kind")] public string? Kind { get; set; }

    [YamlMember(Alias = "source_assembly")] public string? SourceAssembly { get; set; }

    [YamlMember(Alias = "source_member")] public string? SourceMember { get; set; }

    [YamlMember(Alias = "target_assembly")] public string? TargetAssembly { get; set; }

    [YamlMember(Alias = "target_type")] public string? TargetType { get; set; }

    [YamlMember(Alias = "target_member")] public string? TargetMember { get; set; }

    [YamlMember(Alias = "occurrence")] public int? Occurrence { get; set; }

    [YamlMember(Alias = "configuration")] public string? Configuration { get; set; }

    /// <summary>
    /// Builds the structured identity for a version-2 entry. Callers MUST only invoke this on
    /// entries belonging to a version-2 document (guarded by the document's <c>Version</c>).
    /// </summary>
    public ArchitectureViolationIdentity ToIdentity(string contractId)
    {
        return new ArchitectureViolationIdentity(
            IdentityVersion ?? ArchitectureViolationIdentity.CurrentVersion,
            ContractFamily ?? string.Empty,
            Kind ?? string.Empty,
            contractId,
            SourceAssembly,
            SourceType,
            SourceMember,
            TargetAssembly,
            TargetType,
            TargetMember,
            Occurrence ?? 0,
            Configuration);
    }

    public static ArchitectureBaselineIgnoredViolation FromIdentity(
        ArchitectureViolationIdentity identity, string sourceTypeDisplay, string forbiddenReferenceDisplay, string reason)
    {
        return new ArchitectureBaselineIgnoredViolation
        {
            SourceType = sourceTypeDisplay,
            ForbiddenReference = forbiddenReferenceDisplay,
            Reason = reason,
            IdentityVersion = identity.IdentityVersion,
            ContractFamily = identity.ContractFamily,
            Kind = identity.Kind,
            SourceAssembly = identity.SourceAssembly,
            SourceMember = identity.SourceMember,
            TargetAssembly = identity.TargetAssembly,
            TargetType = identity.TargetType,
            TargetMember = identity.TargetMember,
            Occurrence = identity.Occurrence,
            Configuration = identity.Configuration,
        };
    }
}
