using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class RealMsBuildCacheEligibilityEvidenceTests
{
    [Test]
    public void EffectModel_DerivesPhaseShareAmdahlAndAmortizedValues()
    {
        IReadOnlyList<RealMsBuildCacheMeasurement> measurements = CreateMeasurements(includeEligibleControl: true);

        Assert.That(measurements.Single(measurement => measurement.FixtureKind == "real-msbuild" && measurement.Size == "small" && measurement.CacheMode == "disabled").TotalElapsedMilliseconds, Is.EqualTo(100d));
        Assert.That(measurements.Single(measurement => measurement.FixtureKind == "eligible-control" && measurement.Size == "small" && measurement.CacheMode == "disabled").TotalElapsedMilliseconds, Is.EqualTo(50d));

        RealMsBuildCacheEffectEstimate estimate = RealMsBuildCacheEffectModel.Calculate(measurements);

        RealMsBuildCacheEffectPoint small = estimate.Points.Single(point => point.Size == "small");
        Assert.That(small.TargetedPhaseSharePercent, Is.EqualTo(25m));
        Assert.That(small.AmdahlMaximumSpeedup, Is.EqualTo(1.3333333333333333333333333333m).Within(0.0000000000000000000000001m));
        Assert.That(small.ColdMissOverheadPercent, Is.EqualTo(10m));
        Assert.That(small.ExpectedWarmHitReductionPercent, Is.EqualTo(25m));
        Assert.That(small.ExpectedAmortizedReductionPercent, Is.EqualTo(13.3333333333333333333333333333m).Within(0.0000000000000000000000001m));
        Assert.That(small.VerifiedWarmHitObserved, Is.True);
        Assert.That(small.ResourceEvidenceComplete, Is.True);
        Assert.That(estimate.Complete, Is.True);
        estimate.Validate();
    }

    [Test]
    public void EffectModel_LeavesWarmHitAndAmortizedEffectModelOnlyWithoutVerifiedHit()
    {
        RealMsBuildCacheEffectEstimate estimate = RealMsBuildCacheEffectModel.Calculate(
            CreateMeasurements(includeEligibleControl: false));

        Assert.That(estimate.Complete, Is.False);
        Assert.That(estimate.Points, Has.All.Matches<RealMsBuildCacheEffectPoint>(point =>
            !point.VerifiedWarmHitObserved &&
            !point.ResourceEvidenceComplete &&
            point.ColdMissOverheadPercent is null &&
            point.ExpectedWarmHitReductionPercent is null &&
            point.ExpectedAmortizedReductionPercent is null &&
            point.WarmHitAvoidedWork is null));
        estimate.Validate();
    }

    [Test]
    public void Document_RejectsOutcomeAWhileNormalizationGateIsOpen()
    {
        RealMsBuildCacheEligibilityEvidenceDocument document = CreateDocument("A", phase2Authorized: false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(document.Validate)!;

        Assert.That(exception.Message, Does.Contain("completed #991 normalization authority"));
    }

    [Test]
    public void Document_AllowsConservativeOutcomeCAndPreservesFailClosedRows()
    {
        RealMsBuildCacheEligibilityEvidenceDocument document = CreateDocument("C", phase2Authorized: false);

        Assert.DoesNotThrow(document.Validate);
        Assert.DoesNotThrow(() => RealMsBuildCacheEligibilityEvidenceSerialization.Serialize(document));
        string markdown = RealMsBuildCacheEligibilityEvidenceMarkdown.Render(document);
        Assert.That(markdown, Does.Contain("Phase 1 outcome: **C**"));
        Assert.That(markdown, Does.Contain("#991"));
        Assert.That(markdown, Does.Contain("OpenSpec: not applicable"));
        Assert.That(markdown, Does.Contain("eligible-control"));
        Assert.That(markdown, Does.Contain("The targeted boundary includes assembly/artifact loading and analysis phases"));
        Assert.That(markdown, Does.Contain("Without a verified warm-hit control, warm-hit and amortized reductions remain unavailable/model-only"));
        Assert.That(markdown, Does.Contain("Cold/miss overhead is computed only from the eligible-control disabled-versus-population path"));
        Assert.That(markdown, Does.Contain("its absolute cost is normalized against the real-MSBuild disabled baseline"));
        Assert.That(markdown, Does.Contain("Expected warm-hit reduction is normalized to the real-MSBuild denominator"));
        Assert.That(markdown, Does.Contain("Allocated bytes"));
        Assert.That(markdown, Does.Contain("Peak working set"));
    }

    [Test]
    public void Document_RejectsOutcomeCWithoutVerifiedWarmHit()
    {
        RealMsBuildCacheEligibilityEvidenceDocument document = CreateDocument(
            "C",
            phase2Authorized: false,
            measurements: CreateMeasurements(includeEligibleControl: false));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(document.Validate)!;

        Assert.That(exception.Message, Does.Contain("verified warm-hit effect estimate"));
    }

    [Test]
    public void Document_RejectsOutcomeAWhenAmortizedBenefitMissesSuccessThreshold()
    {
        IReadOnlyList<RealMsBuildCacheMeasurement> measurements = CreateMeasurements(includeEligibleControl: true)
            .Select(measurement => measurement.FixtureKind == "eligible-control" && measurement.CacheMode == "repeat"
                ? measurement with { TotalElapsedMilliseconds = measurement.TotalElapsedMilliseconds!.Value * 1.98 }
                : measurement)
            .ToArray();
        RealMsBuildCacheEligibilityEvidenceDocument document = CreateDocument("A", phase2Authorized: true, measurements: measurements);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(document.Validate)!;

        Assert.That(exception.Message, Does.Contain("material amortized effect"));
    }

    [Test]
    public void Document_RejectsOutcomeAWhenEligibleControlCanonicalResultsDiffer()
    {
        IReadOnlyList<RealMsBuildCacheMeasurement> measurements = CreateMeasurements(includeEligibleControl: true)
            .Select(measurement => measurement.FixtureKind == "eligible-control" && measurement.CacheMode == "repeat"
                ? measurement with { CanonicalResultSha256 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" }
                : measurement)
            .ToArray();
        RealMsBuildCacheEligibilityEvidenceDocument document = CreateDocument("A", phase2Authorized: true, measurements: measurements);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(document.Validate)!;

        Assert.That(exception.Message, Does.Contain("Eligible-control evidence"));
    }

    [Test]
    public void EffectModel_LeavesEstimateIncompleteWithoutControlResourceObservations()
    {
        IReadOnlyList<RealMsBuildCacheMeasurement> measurements = CreateMeasurements(includeEligibleControl: true)
            .Select(measurement => measurement.FixtureKind == "eligible-control" && measurement.CacheMode == "repeat"
                ? measurement with { AllocatedBytes = null }
                : measurement)
            .ToArray();

        RealMsBuildCacheEffectEstimate estimate = RealMsBuildCacheEffectModel.Calculate(measurements);

        Assert.That(estimate.Complete, Is.False);
        Assert.That(estimate.Points, Has.All.Matches<RealMsBuildCacheEffectPoint>(point => !point.ResourceEvidenceComplete));
    }

    [Test]
    public void Document_RejectsEligibleControlWithoutExactCacheModes()
    {
        IReadOnlyList<RealMsBuildCacheMeasurement> measurements = CreateMeasurements(includeEligibleControl: true)
            .Where(measurement => !(measurement.FixtureKind == "eligible-control" && measurement.CacheMode == "population"))
            .ToArray();
        RealMsBuildCacheEligibilityEvidenceDocument document = CreateDocument("C", phase2Authorized: false, measurements: measurements);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(document.Validate)!;

        Assert.That(exception.Message, Does.Contain("exactly disabled, population, and repeat"));
    }

    [Test]
    public void Document_AllowsOutcomeAWhenAmortizedBenefitMeetsSuccessThreshold()
    {
        RealMsBuildCacheEligibilityEvidenceDocument document = CreateDocument("A", phase2Authorized: true);

        Assert.DoesNotThrow(document.Validate);
    }

    [Test]
    public void Document_RejectsStaleInputEvidenceWithoutRequiredFailClosedCategories()
    {
        RealMsBuildCacheEligibilityEvidenceDocument document = CreateDocument("C", phase2Authorized: false) with
        {
            StaleInputChecks =
            [
                new() { ChangeKind = "project", Disposition = "reject", Evidence = "Project change rejects reuse." },
                new() { ChangeKind = "source", Disposition = "reject", Evidence = "Source change rejects reuse." },
                new() { ChangeKind = "package", Disposition = "reject", Evidence = "Package change rejects reuse." },
                new() { ChangeKind = "artifact", Disposition = "accept", Evidence = "Artifact change was accepted." },
            ],
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(document.Validate)!;

        Assert.That(exception.Message, Does.Contain("Stale-input evidence"));
    }

    private static RealMsBuildCacheEligibilityEvidenceDocument CreateDocument(
        string decision,
        bool phase2Authorized,
        IReadOnlyList<RealMsBuildCacheMeasurement>? measurements = null)
    {
        measurements ??= CreateMeasurements(includeEligibleControl: true);
        return new RealMsBuildCacheEligibilityEvidenceDocument
        {
            EvidenceSchemaId = RealMsBuildCacheEligibilityEvidenceDocument.SchemaId,
            IssueReference = "#675",
            SourceIdentity = "synthetic-current-tree",
            Runtime = ".NET 10",
            OperatingSystem = "test",
            Architecture = "x64",
            Configuration = "Debug",
            ToolIdentity = "ArchLinterNet.Cli analysis-profile/v1",
            NormalizationGate = new RealMsBuildNormalizationGate
            {
                IssueReference = "#991",
                Status = phase2Authorized ? "complete" : "open",
                Phase2Authorized = phase2Authorized,
                Evidence = "The adopter normalization issue remains the Phase 2 authority.",
            },
            Decision = decision,
            DecisionRationale = "Current evidence does not prove distinct material value before the normalized gate.",
            ReferenceBaseDisposition = new RealMsBuildReferenceBaseDisposition
            {
                Disposition = "routed-to-owning-lane",
                CountedInEffectEstimate = false,
                Evidence = "The current exact-request cache boundary is separate from prepared-analysis reference work; route that work to the owning lane.",
            },
            EffectEstimate = RealMsBuildCacheEffectModel.Calculate(measurements),
            Measurements = measurements,
            StaleInputChecks =
            [
                new() { ChangeKind = "project", Disposition = "reject", Evidence = "Project manifest digest mismatch rejects reuse." },
                new() { ChangeKind = "source", Disposition = "reject", Evidence = "Source inputs remain fail-closed when outside the exact manifest." },
                new() { ChangeKind = "package", Disposition = "reject", Evidence = "Package/framework/reference identity changes reject reuse." },
                new() { ChangeKind = "configuration", Disposition = "reject", Evidence = "Configuration/TFM/platform/RID changes reject reuse." },
                new() { ChangeKind = "artifact", Disposition = "reject", Evidence = "PE/PDB/receipt byte changes reject reuse." },
            ],
            Phase2Routing = "Do not begin Phase 2 eligibility implementation until #991 completes and normalized workflows are remeasured.",
        };
    }

    private static IReadOnlyList<RealMsBuildCacheMeasurement> CreateMeasurements(bool includeEligibleControl)
    {
        List<RealMsBuildCacheMeasurement> measurements = [];
        foreach ((string size, int projects, double total) in new[]
        {
            ("small", 2, 100d),
            ("medium", 4, 200d),
            ("large", 6, 400d),
        })
        {
            string workloadId = $"synthetic-{size}";
            foreach ((string mode, long hits, long writes, double duration) in new[]
            {
                ("disabled", 0, 0, total),
                ("population", 0, 0, total * 1.8),
                ("repeat", 0, 0, total * 1.1),
            })
            {
                measurements.Add(CreateMeasurement("real-msbuild", mode, workloadId, size, projects, hits, writes, duration, total * .25));
            }

            if (includeEligibleControl)
            {
                measurements.Add(CreateMeasurement("eligible-control", "disabled", workloadId, size, projects, 0, 0, total * .5, total * .125, "VerifiedCacheEligible"));
                measurements.Add(CreateMeasurement("eligible-control", "population", workloadId, size, projects, 0, 1, total * .6, total * .125, "VerifiedCacheEligible"));
                measurements.Add(CreateMeasurement("eligible-control", "repeat", workloadId, size, projects, 1, 0, total * .25, total * .125, "VerifiedCacheEligible", avoidedWork: 4));
            }
        }

        return measurements;
    }

    private static RealMsBuildCacheMeasurement CreateMeasurement(
        string fixtureKind,
        string cacheMode,
        string workloadId,
        string size,
        int projectCount,
        long hits,
        long writes,
        double duration,
        double targetedPhase,
        string eligibility = "CacheIneligible",
        long avoidedWork = 0) =>
        new()
        {
            FixtureKind = fixtureKind,
            CacheMode = cacheMode,
            WorkloadId = workloadId,
            WorkloadIdentity = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(workloadId))),
            Size = size,
            ProjectCount = projectCount,
            Eligibility = eligibility,
            IneligibilityReasons = eligibility == "CacheIneligible" ? ["package-reference-identity-unverified"] : [],
            Lookups = cacheMode == "disabled" ? 0 : 1,
            Hits = hits,
            Misses = cacheMode == "population" ? 1 : 0,
            Rejects = 0,
            Writes = writes,
            IneligibleUnitCount = eligibility == "CacheIneligible" ? projectCount : 0,
            BytesRead = cacheMode == "disabled" ? 0 : 100,
            BytesWritten = writes == 0 ? 0 : 100,
            AvoidedWork = avoidedWork,
            DeterministicWork = 8,
            TotalElapsedMilliseconds = duration,
            TargetedPhaseMilliseconds = targetedPhase,
            TargetedPhaseSharePercent = targetedPhase / duration * 100,
            AllocatedBytes = 10,
            PeakWorkingSetBytes = 20,
            CanonicalResultSha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            CompletionStatus = "Completed",
            ExitCode = 0,
        };
}
