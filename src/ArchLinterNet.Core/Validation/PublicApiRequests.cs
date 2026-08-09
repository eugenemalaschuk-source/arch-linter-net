using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Validation;

// All four public-api operations resolve one named contract from one policy, so they share the same
// three inputs. Each request stays a distinct type (rather than one request with a mode flag) so an
// operation-specific option like DryRun cannot be silently passed to an operation that ignores it.
//
// Every destination/source path is the *authored* string; the seam resolves it against the policy
// boundary and hands the resolved absolute path back on the outcome. Hosts must write to that
// resolved path, never re-resolve the authored one against their working directory.
public sealed record PublicApiCaptureRequest
{
    public required string PolicyPath { get; init; }

    public required string ContractId { get; init; }

    public required string OutputPath { get; init; }

    public string? ConditionSetName { get; init; }

    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;
}

public sealed record PublicApiDiffRequest
{
    public required string PolicyPath { get; init; }

    public required string ContractId { get; init; }

    public required string SnapshotPath { get; init; }

    public string? ConditionSetName { get; init; }

    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;
}

public sealed record PublicApiUpdateRequest
{
    public required string PolicyPath { get; init; }

    public required string ContractId { get; init; }

    public required string SnapshotPath { get; init; }

    public bool DryRun { get; init; }

    public string? ConditionSetName { get; init; }

    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;
}

public sealed record PublicApiMigrateRequest
{
    public required string PolicyPath { get; init; }

    public required string ContractId { get; init; }

    public required string OutputPath { get; init; }

    public bool AcceptDrift { get; init; }

    public string? ConditionSetName { get; init; }

    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    public bool NoRestore { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;
}
