namespace ArchLinterNet.Cli.Commands.Badge.Application;

/// <summary>Options for the authenticated badge Relay lifecycle boundary.</summary>
internal sealed record BadgeLifecycleCommandOptions(
    string Operation,
    string? InputPath,
    string? Alias,
    bool DryRun,
    string Format,
    bool ShowHelp,
    long? ExpectedGeneration = null,
    long? ExpectedRevocationEpoch = null,
    long? ExpectedRegistryRevision = null,
    long? ExpectedBarrierEpoch = null,
    string? NewOwner = null,
    string? NewRepository = null,
    string? NewAlias = null,
    string? WorkflowSha = null,
    string? Audience = null,
    string? Bundle = null,
    string? ContractVersion = null,
    string? CompatibilityPlan = null,
    string? Manifest = null,
    long? ExpectedEpoch = null,
    string? TargetDigest = null,
    bool ApproveWithdrawal = false,
    bool ApproveRecovery = false,
    string? WorkflowRef = null);
