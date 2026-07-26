using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

public sealed record BaselineUpdateOutcome(
    bool Succeeded,
    string? Yaml,
    int PreservedCount,
    int NewCount,
    IReadOnlyCollection<ArchitectureViolation> ConfigurationViolations)
{
    public IReadOnlyList<BaselineLifecycleEntry> Entries { get; init; } =
        Array.Empty<BaselineLifecycleEntry>();

    /// <summary>
    /// Non-null when the existing file carries comments a rewrite cannot re-anchor. Classification
    /// and <c>--dry-run</c> reporting still work; only the write is refused, by the caller.
    /// </summary>
    public string? CommentDiagnostic { get; init; }

    public string? Error { get; init; }
}
