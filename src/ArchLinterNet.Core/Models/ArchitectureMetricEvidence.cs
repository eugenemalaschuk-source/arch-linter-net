namespace ArchLinterNet.Core.Model;

/// <summary>
/// Native evidence attached to a shared applicability record for one metric. An unassessable
/// record deliberately carries no value or contributors, so consumers cannot mistake a known
/// subset for a trusted measurement.
/// </summary>
public sealed record ArchitectureMetricEvidence(
    string MetricId,
    string Kind,
    string? NativeSubject,
    string? Unit,
    string EffectiveScope,
    int? Value,
    IReadOnlyList<string>? Contributors)
{
    public IReadOnlyList<string>? Contributors { get; init; } = Value is null
        ? null
        : (Contributors ?? throw new ArgumentException(
                "Complete metric evidence must provide contributor identities.", nameof(Contributors)))
            .OrderBy(contributor => contributor, StringComparer.Ordinal)
            .ToArray();

    public int? ContributorCount => Value is null ? null : Contributors!.Count;

    public bool IsComplete => Value is not null;
}
