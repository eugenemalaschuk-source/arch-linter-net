namespace ArchLinterNet.Core.PolicyContext;

/// <summary>Requests an AI-safe summary of one effective architecture policy.</summary>
public sealed class ArchitecturePolicyContextRequest
{
    /// <summary>Gets or initializes the selected root policy path.</summary>
    public string PolicyPath { get; init; } = string.Empty;
}

/// <summary>Versioned, deterministic facts from an effective architecture policy.</summary>
public sealed record ArchitecturePolicyContextExport(
    int SchemaVersion,
    string Kind,
    ArchitecturePolicyContextPolicy Policy,
    IReadOnlyList<ArchitecturePolicyContextSource> Sources,
    IReadOnlyList<ArchitecturePolicyContextLayer> Layers,
    IReadOnlyList<ArchitecturePolicyContextContract> Contracts,
    IReadOnlyList<ArchitecturePolicyContextClassification> Classification,
    IReadOnlyList<string> SemanticRoles,
    IReadOnlyList<ArchitecturePolicyContextValue> Contexts,
    IReadOnlyList<ArchitecturePolicyContextSourceSet> SourceSets,
    IReadOnlyList<ArchitecturePolicyContextException> Exceptions,
    IReadOnlyList<string> Guidance);

/// <summary>Identifies the effective policy that produced a context export.</summary>
public sealed record ArchitecturePolicyContextPolicy(string Name, int Version, string RootPath, bool HasImports);

/// <summary>Describes one portable source contributing to the effective policy.</summary>
public sealed record ArchitecturePolicyContextSource(
    string Path,
    string Role,
    int Order,
    string? DeclaringPath,
    string? AuthoredImportPath,
    IReadOnlyList<string> ImportChain);

/// <summary>Describes one declared layer and its semantic selector.</summary>
public sealed record ArchitecturePolicyContextLayer(
    string Name,
    string Namespace,
    string NamespaceSuffix,
    bool External,
    ArchitecturePolicyContextSelector? Selector,
    IReadOnlyList<ArchitecturePolicyContextException> Exclusions,
    ArchitecturePolicyContextProvenance? Provenance);

/// <summary>Describes one active strict or audit contract.</summary>
public sealed record ArchitecturePolicyContextContract(
    string Mode,
    string Family,
    string Id,
    string Name,
    string? AuthoredId,
    string? Reason,
    IReadOnlyList<ArchitecturePolicyContextReference> References,
    IReadOnlyList<ArchitecturePolicyContextSelector> Selectors,
    IReadOnlyList<ArchitecturePolicyContextSelector> Exclusions,
    IReadOnlyList<string> CoverageScopes,
    ArchitecturePolicyContextProvenance? Provenance);

/// <summary>Describes one named relationship from a declared contract.</summary>
public sealed record ArchitecturePolicyContextReference(string Kind, IReadOnlyList<string> Values);

/// <summary>Describes a semantic role selector using only declared policy facts.</summary>
public sealed record ArchitecturePolicyContextSelector(
    string Kind,
    string Role,
    IReadOnlyDictionary<string, string> Metadata,
    string? When);

/// <summary>Describes one semantic classification rule.</summary>
public sealed record ArchitecturePolicyContextClassification(
    string Source,
    string Match,
    string Role,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>Describes declared values for one semantic metadata key.</summary>
public sealed record ArchitecturePolicyContextValue(string Key, IReadOnlyList<string> Values);

/// <summary>Describes an authored source set after effective-policy expansion.</summary>
public sealed record ArchitecturePolicyContextSourceSet(
    string Name,
    string Kind,
    IReadOnlyList<string> ResolvedSources,
    bool Optional,
    string Reason,
    ArchitecturePolicyContextProvenance? Provenance);

/// <summary>Describes a narrow declared exclusion or ignored-policy exception.</summary>
public sealed record ArchitecturePolicyContextException(
    string Scope,
    string Subject,
    string Kind,
    string Details,
    string? Reason);

/// <summary>Describes a portable location in the effective policy graph.</summary>
public sealed record ArchitecturePolicyContextProvenance(
    string SourcePath,
    string RootPath,
    string Role,
    string YamlPath,
    int SourceOrder);
