namespace ArchLinterNet.Core.Model;

/// <summary>Outcome of temporal revalidation for one publication evidence envelope.</summary>
internal enum ArchitectureHealthTemporalPublicationReceiptState
{
    Ready,
    Unassessable,
}

/// <summary>Stable reason retained when temporal publication evidence cannot be proven.</summary>
internal sealed record ArchitectureHealthTemporalPublicationReceiptReason(string Code, string Detail);

/// <summary>
/// Core-owned, identity-bound receipt containing only the refreshed temporal publication facts.
/// </summary>
internal sealed record ArchitectureHealthTemporalPublicationReceipt(
    ArchitectureHealthTemporalPublicationReceiptState State,
    DateOnly EvaluationDate,
    DateTimeOffset? SemanticHorizon,
    string SourceHealthSha256,
    string BadgePayloadSha256,
    string MergedTreeSha,
    string ProducerIdentitySha256,
    IReadOnlyList<ArchitectureHealthTemporalPublicationReceiptReason> Reasons)
{
    internal const string CurrentSchemaId = "architecture-health-temporal-publication-receipt/v1";

    internal bool IsReady => State == ArchitectureHealthTemporalPublicationReceiptState.Ready
        && SemanticHorizon is not null
        && Reasons.Count == 0;

    internal IReadOnlyList<ArchitectureHealthTemporalPublicationReceiptReason> Reasons { get; init; } =
        (Reasons ?? throw new ArgumentNullException(nameof(Reasons)))
        .OrderBy(reason => reason.Code, StringComparer.Ordinal)
        .ThenBy(reason => reason.Detail, StringComparer.Ordinal)
        .ToArray();
}

