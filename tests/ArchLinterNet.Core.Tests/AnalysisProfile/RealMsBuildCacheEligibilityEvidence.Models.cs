using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Tests;

internal sealed record RealMsBuildCacheEligibilityEvidenceDocument
{
    public const string SchemaId = "real-msbuild-cache-eligibility/v1";

    public required string EvidenceSchemaId { get; init; }

    public required string IssueReference { get; init; }

    public required string SourceIdentity { get; init; }

    public required string Runtime { get; init; }

    public required string OperatingSystem { get; init; }

    public required string Architecture { get; init; }

    public required string Configuration { get; init; }

    public required string ToolIdentity { get; init; }

    public required RealMsBuildNormalizationGate NormalizationGate { get; init; }

    public required string Decision { get; init; }

    public required string DecisionRationale { get; init; }

    public required RealMsBuildReferenceBaseDisposition ReferenceBaseDisposition { get; init; }

    public required RealMsBuildCacheEffectEstimate EffectEstimate { get; init; }

    public required IReadOnlyList<RealMsBuildCacheMeasurement> Measurements { get; init; }

    public required IReadOnlyList<RealMsBuildStaleInputCheck> StaleInputChecks { get; init; }

    public required string Phase2Routing { get; init; }

    public void Validate()
    {
        Require(EvidenceSchemaId == SchemaId, "Evidence schema id is not canonical.");
        Require(IssueReference == "#675", "Evidence must identify issue #675.");
        Require(SourceIdentity == "synthetic-current-tree", "Evidence source must be synthetic/anonymized.");
        Require(!ContainsPrivateIdentity(SourceIdentity) && !ContainsPrivateIdentity(DecisionRationale),
            "Evidence must not contain private adopter identity or topology.");
        Require(Decision is "A" or "B" or "C", "Decision must be exactly A, B, or C.");
        Require(Measurements.Count > 0, "Evidence must contain measurements.");
        NormalizationGate.Validate();
        ReferenceBaseDisposition.Validate();
        EffectEstimate.Validate();

        string[] sizes = ["small", "medium", "large"];
        string[] requiredCacheModes = ["disabled", "population", "repeat"];
        foreach (string size in sizes)
        {
            IReadOnlyList<RealMsBuildCacheMeasurement> realMeasurements = Measurements
                .Where(measurement => measurement.FixtureKind == "real-msbuild" && measurement.Size == size)
                .ToList();
            Require(realMeasurements.Select(measurement => measurement.CacheMode).OrderBy(mode => mode, StringComparer.Ordinal)
                    .SequenceEqual(requiredCacheModes, StringComparer.Ordinal),
                $"Real-MSBuild evidence for {size} must contain exactly disabled, population, and repeat modes.");
            Require(realMeasurements.All(measurement => measurement.Eligibility == "CacheIneligible"),
                $"Real-MSBuild evidence for {size} must retain the current fail-closed eligibility result.");
            Require(realMeasurements.All(measurement => measurement.IneligibilityReasons.Count > 0),
                $"Real-MSBuild evidence for {size} must retain typed ineligibility reasons.");
            Require(realMeasurements.Select(measurement => measurement.CanonicalResultSha256).Distinct(StringComparer.Ordinal).Count() == 1,
                $"Real-MSBuild evidence for {size} must preserve canonical result identity across cache modes.");
            Require(realMeasurements.All(measurement => measurement.Hits == 0),
                $"Ineligible real-MSBuild evidence for {size} cannot claim a cache hit.");

            IReadOnlyList<RealMsBuildCacheMeasurement> controlMeasurements = Measurements
                .Where(measurement => measurement.FixtureKind == "eligible-control" && measurement.Size == size)
                .ToList();
            if (controlMeasurements.Count > 0)
            {
                Require(controlMeasurements.Select(measurement => measurement.CacheMode).OrderBy(mode => mode, StringComparer.Ordinal)
                        .SequenceEqual(requiredCacheModes, StringComparer.Ordinal),
                    $"Eligible-control evidence for {size} must contain exactly disabled, population, and repeat modes.");
                Require(controlMeasurements.Select(measurement => measurement.CanonicalResultSha256)
                        .Distinct(StringComparer.Ordinal).Count() == 1,
                    $"Eligible-control evidence for {size} must preserve canonical result identity across cache modes.");
            }
        }

        string[] requiredStaleInputKinds = ["project", "source", "package", "configuration", "artifact"];
        foreach (RealMsBuildStaleInputCheck check in StaleInputChecks)
        {
            check.Validate();
        }

        Require(StaleInputChecks.Count == requiredStaleInputKinds.Length &&
            requiredStaleInputKinds.All(kind => StaleInputChecks.Any(check => check.ChangeKind == kind)),
            "Evidence must cover exactly project, source, package, configuration, and artifact changes.");
        Require(StaleInputChecks.Select(check => check.ChangeKind).Distinct(StringComparer.Ordinal).Count() == StaleInputChecks.Count,
            "Stale-input checks must have unique change kinds.");
        Require(StaleInputChecks.All(check => check.Disposition == "reject"),
            "Every stale-input check must demonstrate a fail-closed reject disposition.");
        Require(Phase2Routing.Contains("#991", StringComparison.Ordinal),
            "Phase 2 routing must name the #991 normalization gate.");

        if (Decision == "A")
        {
            Require(NormalizationGate.Phase2Authorized && NormalizationGate.Status == "complete",
                "Outcome A requires completed #991 normalization authority.");
            Require(EffectEstimate.Complete, "Outcome A requires a complete effect estimate.");
            Require(EffectEstimate.Points.All(point =>
                    point.ExpectedAmortizedReductionPercent.HasValue &&
                    point.ExpectedAmortizedReductionPercent.Value >= EffectEstimate.SuccessThresholdPercent),
                "Outcome A requires a material amortized effect at or above the success threshold for every representative size.");
            Require(Measurements.Any(measurement => measurement.FixtureKind == "eligible-control" && measurement.Hits > 0),
                "Outcome A requires an observed verified warm-hit control.");
            Require(ReferenceBaseDisposition.CountedInEffectEstimate == false,
                "Reference/base work cannot be counted as candidate exact-cache savings.");
        }

        if (Decision == "C")
        {
            Require(EffectEstimate.Complete,
                "Outcome C cannot be final while the verified warm-hit effect estimate is incomplete or model-only.");
        }
    }

    private static bool ContainsPrivateIdentity(string value) =>
        value.Contains("firstice", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("github.com/", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("namespace", StringComparison.OrdinalIgnoreCase);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

internal sealed record RealMsBuildNormalizationGate
{
    public required string IssueReference { get; init; }

    public required string Status { get; init; }

    public required bool Phase2Authorized { get; init; }

    public required string Evidence { get; init; }

    public void Validate()
    {
        if (IssueReference != "#991" || Status is not ("open" or "complete") ||
            (Status != "complete" && Phase2Authorized) || string.IsNullOrWhiteSpace(Evidence))
        {
            throw new InvalidOperationException("The #991 normalization gate must be explicit and fail closed.");
        }
    }
}

internal sealed record RealMsBuildReferenceBaseDisposition
{
    public required string Disposition { get; init; }

    public required bool CountedInEffectEstimate { get; init; }

    public required string Evidence { get; init; }

    public void Validate()
    {
        if (Disposition is not ("not-applicable-current-cache-boundary" or "dominated-by-prepared-analysis" or "routed-to-owning-lane") ||
            CountedInEffectEstimate || string.IsNullOrWhiteSpace(Evidence) ||
            Evidence.Contains("#492", StringComparison.Ordinal) && !Evidence.Contains("separate", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Reference/base exact-request evidence must be explicit and disjoint from prepared-state savings.");
        }
    }
}

internal sealed record RealMsBuildCacheEffectEstimate
{
    public required string TargetedPhase { get; init; }

    public required string TargetedWork { get; init; }

    public required decimal SuccessThresholdPercent { get; init; }

    public required decimal KillCriterionPercent { get; init; }

    public required int ExpectedReuseCount { get; init; }

    public required string ReuseAssumptions { get; init; }

    public required bool Complete { get; init; }

    public required IReadOnlyList<RealMsBuildCacheEffectPoint> Points { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TargetedPhase) || string.IsNullOrWhiteSpace(TargetedWork) ||
            SuccessThresholdPercent <= 0 || KillCriterionPercent < 0 || KillCriterionPercent >= SuccessThresholdPercent ||
            ExpectedReuseCount < 2 || string.IsNullOrWhiteSpace(ReuseAssumptions) || Points.Count != 3 ||
            Points.Select(point => point.Size).Distinct(StringComparer.Ordinal).Count() != 3 ||
            !new[] { "small", "medium", "large" }.All(size => Points.Any(point => point.Size == size)))
        {
            throw new InvalidOperationException("The cache effect estimate must contain valid S/M/L thresholds and assumptions.");
        }

        foreach (RealMsBuildCacheEffectPoint point in Points)
        {
            point.Validate();
        }
    }
}

internal sealed record RealMsBuildCacheEffectPoint
{
    public required string Size { get; init; }

    public required decimal TargetedPhaseSharePercent { get; init; }

    public required decimal AmdahlMaximumSpeedup { get; init; }

    public required decimal? ColdMissOverheadPercent { get; init; }

    public required decimal? ExpectedWarmHitReductionPercent { get; init; }

    public required decimal? ExpectedAmortizedReductionPercent { get; init; }

    public required long? WarmHitAvoidedWork { get; init; }

    public required bool VerifiedWarmHitObserved { get; init; }

    public void Validate()
    {
        if (Size is not ("small" or "medium" or "large") ||
            TargetedPhaseSharePercent < 0 || TargetedPhaseSharePercent > 100 ||
            AmdahlMaximumSpeedup < 1 ||
            (ColdMissOverheadPercent is < 0 or > 100) ||
            (ExpectedWarmHitReductionPercent is < 0 or > 100) ||
            (ExpectedAmortizedReductionPercent is < 0 or > 100) || WarmHitAvoidedWork < 0 ||
            (!VerifiedWarmHitObserved &&
                (ExpectedWarmHitReductionPercent.HasValue || ExpectedAmortizedReductionPercent.HasValue ||
                    WarmHitAvoidedWork.HasValue)) ||
            (VerifiedWarmHitObserved &&
                (!ExpectedWarmHitReductionPercent.HasValue || !ExpectedAmortizedReductionPercent.HasValue ||
                    !WarmHitAvoidedWork.HasValue)))
        {
            throw new InvalidOperationException($"Invalid cache effect point for {Size}.");
        }
    }
}

internal sealed record RealMsBuildCacheMeasurement
{
    public required string FixtureKind { get; init; }

    public required string CacheMode { get; init; }

    public required string WorkloadId { get; init; }

    public required string WorkloadIdentity { get; init; }

    public required string Size { get; init; }

    public required int ProjectCount { get; init; }

    public required string Eligibility { get; init; }

    public required IReadOnlyList<string> IneligibilityReasons { get; init; }

    public required long Lookups { get; init; }

    public required long Hits { get; init; }

    public required long Misses { get; init; }

    public required long Rejects { get; init; }

    public required long Writes { get; init; }

    public required long IneligibleUnitCount { get; init; }

    public required long? BytesRead { get; init; }

    public required long? BytesWritten { get; init; }

    public required long? AvoidedWork { get; init; }

    public required long DeterministicWork { get; init; }

    public required double? TotalElapsedMilliseconds { get; init; }

    public required double? TargetedPhaseMilliseconds { get; init; }

    public required double? TargetedPhaseSharePercent { get; init; }

    public required long? AllocatedBytes { get; init; }

    public required long? PeakWorkingSetBytes { get; init; }

    public required string CanonicalResultSha256 { get; init; }

    public required string CompletionStatus { get; init; }

    public required int ExitCode { get; init; }

    [JsonIgnore]
    public string IdentityKey => $"{FixtureKind}:{Size}:{CacheMode}";
}

internal sealed record RealMsBuildStaleInputCheck
{
    public required string ChangeKind { get; init; }

    public required string Disposition { get; init; }

    public required string Evidence { get; init; }

    public void Validate()
    {
        if (ChangeKind is not ("project" or "source" or "package" or "configuration" or "artifact") ||
            Disposition != "reject" || string.IsNullOrWhiteSpace(Evidence))
        {
            throw new InvalidOperationException($"Stale-input evidence for {ChangeKind} must be an explicit fail-closed reject.");
        }
    }
}

internal static class RealMsBuildCacheEffectModel
{
    public static RealMsBuildCacheEffectEstimate Calculate(
        IReadOnlyList<RealMsBuildCacheMeasurement> measurements,
        int expectedReuseCount = 3)
    {
        List<RealMsBuildCacheEffectPoint> points = [];
        foreach (string size in new[] { "small", "medium", "large" })
        {
            RealMsBuildCacheMeasurement baseline = measurements.Single(
                measurement => measurement.FixtureKind == "real-msbuild" && measurement.Size == size && measurement.CacheMode == "disabled");
            double total = baseline.TotalElapsedMilliseconds.GetValueOrDefault();
            double targeted = baseline.TargetedPhaseMilliseconds.GetValueOrDefault();
            decimal share = total > 0 ? (decimal)(targeted / total * 100) : 0;
            RealMsBuildCacheMeasurement? controlBaseline = measurements.SingleOrDefault(
                measurement => measurement.FixtureKind == "eligible-control" && measurement.Size == size && measurement.CacheMode == "disabled");
            RealMsBuildCacheMeasurement? controlPopulation = measurements.SingleOrDefault(
                measurement => measurement.FixtureKind == "eligible-control" && measurement.Size == size && measurement.CacheMode == "population");
            RealMsBuildCacheMeasurement? controlHit = measurements.SingleOrDefault(
                measurement => measurement.FixtureKind == "eligible-control" && measurement.Size == size && measurement.CacheMode == "repeat" && measurement.Hits > 0);
            bool controlPopulationVerified = controlPopulation is
            {
                Eligibility: "VerifiedCacheEligible",
                Misses: > 0,
                Writes: > 0,
                Rejects: 0,
            };
            decimal? coldOverhead = controlPopulationVerified && controlBaseline?.TotalElapsedMilliseconds is > 0 &&
                controlPopulation!.TotalElapsedMilliseconds.HasValue
                ? Math.Max(0, (decimal)((controlPopulation.TotalElapsedMilliseconds.Value - controlBaseline.TotalElapsedMilliseconds!.Value) /
                    controlBaseline.TotalElapsedMilliseconds.Value * 100))
                : null;
            bool controlCanonicalResultEquivalent = controlBaseline != null && controlPopulation != null && controlHit != null &&
                controlBaseline.CanonicalResultSha256 == controlPopulation.CanonicalResultSha256 &&
                controlBaseline.CanonicalResultSha256 == controlHit.CanonicalResultSha256;
            bool verifiedWarmHitObserved = controlPopulationVerified && controlCanonicalResultEquivalent &&
                controlBaseline?.Eligibility == "VerifiedCacheEligible" && controlBaseline.TotalElapsedMilliseconds is > 0 &&
                controlHit?.Eligibility == "VerifiedCacheEligible" && controlHit.TotalElapsedMilliseconds is >= 0;
            decimal? warmReduction = verifiedWarmHitObserved
                ? Math.Clamp((decimal)((controlBaseline!.TotalElapsedMilliseconds!.Value - controlHit!.TotalElapsedMilliseconds!.Value) /
                    controlBaseline.TotalElapsedMilliseconds.Value * 100), 0, 100)
                : null;
            decimal? amortized = warmReduction.HasValue && coldOverhead.HasValue
                ? Math.Clamp(((expectedReuseCount - 1) * warmReduction.Value - coldOverhead.Value) / expectedReuseCount, 0, 100)
                : null;
            points.Add(new RealMsBuildCacheEffectPoint
            {
                Size = size,
                TargetedPhaseSharePercent = share,
                AmdahlMaximumSpeedup = share < 100 ? 100m / (100m - share) : decimal.MaxValue,
                ColdMissOverheadPercent = coldOverhead,
                ExpectedWarmHitReductionPercent = warmReduction,
                ExpectedAmortizedReductionPercent = amortized,
                WarmHitAvoidedWork = verifiedWarmHitObserved ? controlHit!.AvoidedWork : null,
                VerifiedWarmHitObserved = verifiedWarmHitObserved,
            });
        }

        return new RealMsBuildCacheEffectEstimate
        {
            TargetedPhase = "cache-avoidable-analysis",
            TargetedWork = "Assembly/artifact loading and analysis phases skipped by a verified exact-request hit; cache lookup, build-state authorization, and output routing remain outside the boundary.",
            SuccessThresholdPercent = 10,
            KillCriterionPercent = 5,
            ExpectedReuseCount = expectedReuseCount,
            ReuseAssumptions = "Three equivalent requests in one workflow or across immutable reference/base revisions; cache misses remain correct fallbacks.",
            Complete = points.All(point => point.TargetedPhaseSharePercent > 0 && point.VerifiedWarmHitObserved && point.ColdMissOverheadPercent.HasValue),
            Points = points,
        };
    }
}
