using ArchLinterNet.Core.BuildState;

namespace ArchLinterNet.Core.Validation;

public sealed record ValidationRequest
{
    public required string PolicyPath { get; init; }

    public required string Mode { get; init; }

    public string? ConditionSetName { get; init; }

    public IReadOnlyList<string>? PreprocessorSymbols { get; init; }

    public IReadOnlyCollection<string>? ContractIds { get; init; }

    public string? BaselinePath { get; init; }

    public bool IncludeAsmdefContracts { get; init; } = true;

    public bool EnforceUnmatchedIgnoredViolationsPolicy { get; init; }

    // Ordinary (default): never restores/builds. EnsureBuilt: explicit opt-in that builds the
    // graph once and verifies it via a build receipt. See BuildPreparationMode.
    public BuildPreparationMode PreparationMode { get; init; } = BuildPreparationMode.Ordinary;

    // Fails closed offline with a `restore-required` diagnostic instead of allowing restore.
    // Composes with PreparationMode: valid with Ordinary (checked, never built) and with
    // EnsureBuilt (checked, then passed through to the build invocation).
    public bool NoRestore { get; init; }

    public string? RequestedConfiguration { get; init; }

    public string? RequestedTargetFramework { get; init; }
}
