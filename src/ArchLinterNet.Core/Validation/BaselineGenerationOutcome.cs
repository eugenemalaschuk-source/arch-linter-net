using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineGenerationOutcome(
    bool Succeeded,
    string? Yaml,
    int CandidateCount,
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations)
{
    /// <summary>Lifecycle report for the proposed document; every entry is <c>added</c>.</summary>
    public IReadOnlyList<BaselineLifecycleEntry> Entries { get; init; } =
        Array.Empty<BaselineLifecycleEntry>();

    /// <summary>Argument-level failure (for example a malformed reason mapping).</summary>
    public string? Error { get; init; }
}
