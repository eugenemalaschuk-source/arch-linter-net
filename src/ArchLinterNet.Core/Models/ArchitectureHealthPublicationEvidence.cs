namespace ArchLinterNet.Core.Model;

/// <summary>Whether canonical Architecture Health evidence has a finite reuse horizon.</summary>
internal enum ArchitectureHealthPublicationEvidenceState
{
    Ready,
    Unassessable,
}

/// <summary>Stable, actionable reason retained when publication evidence cannot be proven.</summary>
internal sealed record ArchitectureHealthPublicationEvidenceReason(string Code, string Detail);

/// <summary>Core-owned validity receipt for bounded publication of one Health outcome.</summary>
internal sealed record ArchitectureHealthPublicationEvidence(
    ArchitectureHealthPublicationEvidenceState State,
    DateTimeOffset? SemanticHorizon,
    IReadOnlyList<ArchitectureHealthPublicationEvidenceReason> Reasons)
{
    internal const string CurrentSchemaId = "architecture-health-publication-evidence/v1";

    internal bool IsReady => State == ArchitectureHealthPublicationEvidenceState.Ready
        && SemanticHorizon is not null
        && Reasons.Count == 0;

    internal IReadOnlyList<ArchitectureHealthPublicationEvidenceReason> Reasons { get; init; } =
        (Reasons ?? throw new ArgumentNullException(nameof(Reasons)))
        .OrderBy(reason => reason.Code, StringComparer.Ordinal)
        .ThenBy(reason => reason.Detail, StringComparer.Ordinal)
        .ToArray();
}
