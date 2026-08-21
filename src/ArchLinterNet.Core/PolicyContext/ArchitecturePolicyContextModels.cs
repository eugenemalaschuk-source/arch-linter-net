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
    ArchitecturePolicyContextGuardrails Guardrails,
    ArchitecturePolicyContextAnalysis Analysis,
    IReadOnlyList<ArchitecturePolicyContextSource> Sources,
    IReadOnlyList<ArchitecturePolicyContextLayer> Layers,
    IReadOnlyList<ArchitecturePolicyContextContract> Contracts,
    IReadOnlyList<ArchitecturePolicyContextClassification> Classification,
    IReadOnlyList<string> SemanticRoles,
    IReadOnlyList<ArchitecturePolicyContextValue> Contexts,
    IReadOnlyList<ArchitecturePolicyContextSourceSet> SourceSets,
    IReadOnlyList<ArchitecturePolicyContextSourceExpansion> SourceExpansions,
    IReadOnlyList<ArchitecturePolicyContextException> Exceptions,
    IReadOnlyList<string> Guidance)
{
    /// <summary>Current supported policy-context schema version.</summary>
    public const int CurrentSchemaVersion = 3;
}

/// <summary>Identifies the effective policy that produced a context export.</summary>
public sealed record ArchitecturePolicyContextPolicy(string Name, int Version, string RootPath, bool HasImports);

/// <summary>Explicit policy configuration for change-time architecture guardrails.</summary>
public sealed record ArchitecturePolicyContextGuardrails(string PolicyWeakening);

/// <summary>Typed static analysis inputs that define declared governed scope.</summary>
public sealed record ArchitecturePolicyContextAnalysis(
    IReadOnlyList<string> TargetAssemblies,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> ProjectInclude,
    IReadOnlyList<string> ProjectExclude,
    IReadOnlyList<string> SourceRoots);

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
    IReadOnlyList<ArchitecturePolicyContextContractFact> Facts,
    IReadOnlyList<ArchitecturePolicyContextSelector> Selectors,
    IReadOnlyList<ArchitecturePolicyContextAdapterBinding> AdapterBindings,
    IReadOnlyList<ArchitecturePolicyContextSelector> Exclusions,
    IReadOnlyList<string> CoverageScopes,
    ArchitecturePolicyContextProvenance? Provenance);

/// <summary>Describes one named relationship from a declared contract.</summary>
public sealed record ArchitecturePolicyContextReference(string Kind, IReadOnlyList<string> Values);

/// <summary>Describes a typed effective-policy rule, including nested rule inputs where needed.</summary>
public sealed record ArchitecturePolicyContextContractFact(
    string Name,
    IReadOnlyList<string> Values,
    IReadOnlyList<ArchitecturePolicyContextContractFact> Items);

/// <summary>Describes a semantic role selector using only declared policy facts.</summary>
public sealed record ArchitecturePolicyContextSelector(
    string Kind,
    string Role,
    IReadOnlyDictionary<string, string> Metadata,
    string? When);

/// <summary>Describes one reviewed adapter-to-port binding in a port boundary.</summary>
public sealed record ArchitecturePolicyContextAdapterBinding(
    ArchitecturePolicyContextSelector Adapter,
    ArchitecturePolicyContextSelector ExpectedPort,
    IReadOnlyList<ArchitecturePolicyContextSelector> AllowedContexts);

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

/// <summary>Describes one authored contract's effective source-set expansion and subtraction evidence.</summary>
public sealed record ArchitecturePolicyContextSourceExpansion(
    string Group,
    string AuthoredContractId,
    string AuthoredContractName,
    string Kind,
    string? SelectorField,
    IReadOnlyList<string> SetNames,
    bool OptionalEmpty,
    string OptionalReason,
    ArchitecturePolicyContextProvenance? Provenance,
    IReadOnlyList<ArchitecturePolicyContextExpandedInstance> Instances,
    IReadOnlyList<ArchitecturePolicyContextExpandedInstance> Inclusions,
    IReadOnlyList<ArchitecturePolicyContextExpandedExclusion> Exclusions);

/// <summary>Describes one source selected for an authored contract before or after subtraction.</summary>
public sealed record ArchitecturePolicyContextExpandedInstance(
    string ContractId,
    string? Source,
    string? SetName,
    string? Selector,
    bool OptionalEmpty,
    string OptionalReason,
    ArchitecturePolicyContextProvenance? Provenance,
    ArchitecturePolicyContextProvenance? AuthoredContractProvenance,
    ArchitecturePolicyContextProvenance? SourceSetReferenceProvenance);

/// <summary>Describes one authored source, source-set, or template-container exclusion.</summary>
public sealed record ArchitecturePolicyContextExpandedExclusion(
    string? Source,
    string? SetName,
    string? Selector,
    bool Matched,
    bool OptionalEmpty,
    string OptionalReason,
    ArchitecturePolicyContextProvenance? Provenance);

/// <summary>Describes a narrow declared exclusion or ignored-policy exception.</summary>
public sealed record ArchitecturePolicyContextException(
    string Scope,
    string Subject,
    string Kind,
    string Details,
    string? Reason)
{
    /// <summary>Gets the typed matcher evidence when this is an ignored-violation exception.</summary>
    public ArchitecturePolicyContextIgnoredViolation? IgnoredViolation { get; init; }
}

/// <summary>Typed source and forbidden-reference matchers for an ignored violation.</summary>
public sealed record ArchitecturePolicyContextIgnoredViolation(string SourceType, string ForbiddenReference);

/// <summary>Describes a portable location in the effective policy graph.</summary>
public sealed record ArchitecturePolicyContextProvenance(
    string SourcePath,
    string RootPath,
    string Role,
    string YamlPath,
    int SourceOrder);
