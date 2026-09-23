using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Core.Tests;

internal static class RealMsBuildCacheEligibilityEvidenceSerialization
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
    };

    public static string Serialize(RealMsBuildCacheEligibilityEvidenceDocument document)
    {
        document.Validate();
        return JsonSerializer.Serialize(document, _options) + Environment.NewLine;
    }
}

internal static class RealMsBuildCacheEligibilityEvidenceMarkdown
{
    public static string Render(RealMsBuildCacheEligibilityEvidenceDocument document)
    {
        document.Validate();
        StringBuilder builder = new StringBuilder()
            .AppendLine("# Real-MSBuild analysis-cache eligibility evidence (#675)")
            .AppendLine()
            .AppendLine("## Decision")
            .AppendLine()
            .Append("Phase 1 decision state: **").Append(document.Decision).AppendLine("**")
            .AppendLine()
            .AppendLine(document.DecisionRationale)
            .AppendLine()
            .AppendLine($"Phase 2 remains gated by {document.NormalizationGate.IssueReference}: status **{document.NormalizationGate.Status}**, authorized: **{document.NormalizationGate.Phase2Authorized}**.")
            .AppendLine(document.Phase2Routing)
            .AppendLine()
            .AppendLine("## Methodology")
            .AppendLine()
            .AppendLine("- Reuse #502's synthetic workload generator/materializer; no second benchmark corpus is created.")
            .AppendLine("- Real-MSBuild rows retain the observed fail-closed eligibility and typed reasons. The separately labelled staged control is reported as unavailable when its artifact authorization cannot produce a verified hit; it is never counted as a real-MSBuild success.")
            .AppendLine("- The targeted boundary includes assembly/artifact loading and analysis phases that a verified exact-request hit skips; cache lookup, build-state authorization and output routing remain outside it. Deterministic counters establish avoided-work scope; Stopwatch values are environment-labelled supporting evidence.")
            .AppendLine("- Without a verified warm-hit control, warm-hit and amortized reductions remain unavailable/model-only and cannot be used to declare a final outcome C.")
            .AppendLine("- Cold/miss overhead is computed only from the eligible-control disabled-versus-population path when eligibility, miss, and cache write are all verified; its absolute cost is normalized against the real-MSBuild disabled baseline, and ineligible or rejected real-MSBuild rows are never used as amortization cost.")
            .AppendLine("- Expected warm-hit reduction is normalized to the real-MSBuild denominator by applying the observed control targeted-work avoidance fraction to the real targeted-phase share; it cannot exceed the real Amdahl bound.")
            .AppendLine("- Outcome-complete resource evidence requires allocation, peak working set, bytes read, and bytes written observations for eligible-control disabled, population, and repeat paths.")
            .AppendLine("- Eligible-control calibration is paired to the corresponding real-MSBuild workload by project count and an explicit calibration-pair identity; mismatches leave the estimate incomplete.")
            .AppendLine("- Cache-disabled, population/miss, and repeat results retain canonical-result identity within each fixture and across eligible-control versus real-MSBuild workloads; stale-input checks retain fail-closed dispositions.")
            .AppendLine("- Decision classification is total after #991: A requires every S/M/L point at or above the success threshold, C requires every point at or below the kill criterion, and B covers complete useful or mixed-scale evidence between those uniform outcomes.")
            .AppendLine("- The exact-request cache estimate excludes prepared-analysis persistence and separately records reference/base-side work.")
            .AppendLine()
            .AppendLine("## Environment")
            .AppendLine()
            .AppendLine($"- Runtime: {document.Runtime}")
            .AppendLine($"- Operating system: {document.OperatingSystem}")
            .AppendLine($"- Architecture: {document.Architecture}")
            .AppendLine($"- Configuration: {document.Configuration}")
            .AppendLine($"- Tool identity: {document.ToolIdentity}")
            .AppendLine($"- Source identity: {document.SourceIdentity}")
            .AppendLine()
            .AppendLine("## Pre-implementation effect estimate")
            .AppendLine()
            .AppendLine($"Targeted phase: **{document.EffectEstimate.TargetedPhase}** — {document.EffectEstimate.TargetedWork}")
            .AppendLine($"Expected equivalent reuse count: **{document.EffectEstimate.ExpectedReuseCount}**. Assumptions: {document.EffectEstimate.ReuseAssumptions}")
            .AppendLine($"Success threshold: **{document.EffectEstimate.SuccessThresholdPercent.ToString("F1", CultureInfo.InvariantCulture)}%** amortized reduction; kill criterion: **{document.EffectEstimate.KillCriterionPercent.ToString("F1", CultureInfo.InvariantCulture)}%**.")
            .AppendLine()
            .AppendLine("| Size | Targeted phase share | Amdahl max speedup | Cold/miss overhead | Expected warm-hit reduction | Expected amortized reduction | Avoided work | Verified hit observed | Resource evidence |")
            .AppendLine("|---|---:|---:|---:|---:|---:|---:|---|---|");
        foreach (RealMsBuildCacheEffectPoint point in document.EffectEstimate.Points.OrderBy(point => SizeOrder(point.Size)))
        {
            builder.Append("| ").Append(point.Size)
                .Append(" | ").Append(point.TargetedPhaseSharePercent.ToString("F2", CultureInfo.InvariantCulture)).Append('%')
                .Append(" | ").Append(point.AmdahlMaximumSpeedup.ToString("F2", CultureInfo.InvariantCulture)).Append('x')
                .Append(" | ").Append(Format(point.ColdMissOverheadPercent)).Append(point.ColdMissOverheadPercent.HasValue ? "%" : "")
                .Append(" | ").Append(Format(point.ExpectedWarmHitReductionPercent)).Append(point.ExpectedWarmHitReductionPercent.HasValue ? "%" : "")
                .Append(" | ").Append(Format(point.ExpectedAmortizedReductionPercent)).Append(point.ExpectedAmortizedReductionPercent.HasValue ? "%" : "")
                .Append(" | ").Append(point.WarmHitAvoidedWork?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")
                .Append(" | ").Append(point.VerifiedWarmHitObserved ? "yes" : "no")
                .Append(" | ").Append(point.ResourceEvidenceComplete ? "complete" : "unavailable")
                .AppendLine(" |");
        }

        builder.AppendLine()
            .AppendLine("## Cache measurements")
            .AppendLine()
            .AppendLine("| Fixture | Size | Mode | Projects / calibration pair | Eligibility | Reasons | Lookups | Hits | Misses | Rejects | Writes | Ineligible units | Bytes read | Bytes written | Allocated bytes | Peak working set | Avoided work | Total ms | Targeted phase ms | Targeted share | Canonical result |")
            .AppendLine("|---|---|---|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (RealMsBuildCacheMeasurement measurement in document.Measurements.OrderBy(measurement => SizeOrder(measurement.Size)).ThenBy(measurement => measurement.FixtureKind, StringComparer.Ordinal).ThenBy(measurement => measurement.CacheMode, StringComparer.Ordinal))
        {
            builder.Append("| ").Append(measurement.FixtureKind)
                .Append(" | ").Append(measurement.Size)
                .Append(" | ").Append(measurement.CacheMode)
                .Append(" | ").Append(measurement.ProjectCount.ToString(CultureInfo.InvariantCulture))
                .Append(" / ").Append(measurement.CalibrationPairIdentity)
                .Append(" | ").Append(measurement.Eligibility)
                .Append(" | ").Append(string.Join(", ", measurement.IneligibilityReasons))
                .Append(" | ").Append(measurement.Lookups.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(measurement.Hits.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(measurement.Misses.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(measurement.Rejects.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(measurement.Writes.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(measurement.IneligibleUnitCount.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(measurement.BytesRead?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")
                .Append(" | ").Append(measurement.BytesWritten?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")
                .Append(" | ").Append(measurement.AllocatedBytes?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")
                .Append(" | ").Append(measurement.PeakWorkingSetBytes?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")
                .Append(" | ").Append(measurement.AvoidedWork?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")
                .Append(" | ").Append(Format(measurement.TotalElapsedMilliseconds))
                .Append(" | ").Append(Format(measurement.TargetedPhaseMilliseconds))
                .Append(" | ").Append(Format(measurement.TargetedPhaseSharePercent)).Append(measurement.TargetedPhaseSharePercent.HasValue ? "%" : "")
                .Append(" | ").Append(measurement.CanonicalResultSha256)
                .AppendLine(" |");
        }

        builder.AppendLine()
            .AppendLine("## Reference/base-side exact-request disposition")
            .AppendLine()
            .Append("- Disposition: **").Append(document.ReferenceBaseDisposition.Disposition).AppendLine("**")
            .AppendLine($"- Counted in candidate exact-cache effect estimate: **{document.ReferenceBaseDisposition.CountedInEffectEstimate}**")
            .AppendLine($"- Evidence: {document.ReferenceBaseDisposition.Evidence}")
            .AppendLine()
            .AppendLine("## Stale-input dispositions")
            .AppendLine()
            .AppendLine("| Change kind | Disposition | Evidence |")
            .AppendLine("|---|---|---|");
        foreach (RealMsBuildStaleInputCheck check in document.StaleInputChecks.OrderBy(check => check.ChangeKind, StringComparer.Ordinal))
        {
            builder.Append("| ").Append(check.ChangeKind).Append(" | ").Append(check.Disposition).Append(" | ").Append(check.Evidence).AppendLine(" |");
        }

        return builder
            .AppendLine()
            .AppendLine("OpenSpec: not applicable. This artifact records internal benchmark/evidence tooling and changes no runtime, policy, cache authorization, public API, or documented user guarantee.")
            .AppendLine("No private adopter identity, repository URL, namespace, proprietary topology, or raw private CI log is included.")
            .ToString();
    }

    private static int SizeOrder(string size) => size switch
    {
        "small" => 0,
        "medium" => 1,
        "large" => 2,
        _ => 3,
    };

    private static string Format(double? value) => value?.ToString("F3", CultureInfo.InvariantCulture) ?? "unavailable";

    private static string Format(decimal? value) => value?.ToString("F2", CultureInfo.InvariantCulture) ?? "unavailable";
}
