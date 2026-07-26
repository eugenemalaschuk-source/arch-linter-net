using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// The lifecycle disposition of each entry an update or prune proposes, plus the guarantees that keep
/// a write reviewable: preserved reason/issue metadata, mapped reasons for new entries only, retained
/// ambiguity, and a reported comment refusal.
/// </summary>
public sealed partial class ArchitectureBaselineApplicationServiceFakeCompositionTests
{
    [Test]
    public void Update_ClassifiesKeptStaleAmbiguousAndAddedEntries()
    {
        BaselineUpdateOutcome outcome = RunLifecycleUpdate(new BaselineUpdateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            Reason = "flat debt",
        });

        Dictionary<BaselineEntryLifecycle, List<string>> bySource = outcome.Entries
            .GroupBy(e => e.Lifecycle)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Entry.SourceType).OrderBy(s => s, StringComparer.Ordinal).ToList());

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(bySource[BaselineEntryLifecycle.Kept], Is.EqualTo(new[] { "SrcKept" }));
            Assert.That(bySource[BaselineEntryLifecycle.Stale], Is.EqualTo(new[] { "SrcStale" }));
            Assert.That(bySource[BaselineEntryLifecycle.Ambiguous], Is.EqualTo(new[] { "SrcAmbiguous" }));
            Assert.That(bySource[BaselineEntryLifecycle.Added], Is.EqualTo(new[] { "SrcNew" }));
        });
    }

    [Test]
    public void Update_KeptEntry_RetainsReasonAndIssueMetadataVerbatim()
    {
        var generator = new FakeBaselineGenerator();
        RunLifecycleUpdate(
            new BaselineUpdateRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                BaselinePath = "unused-by-fakes.baseline.yml",
                Mode = "all",
                Reason = "flat debt",
                // A mapping that targets the very contract the kept entry belongs to must still not
                // rewrite it: mapping applies to added entries only.
                ReasonForContract = ["known-rule=mapped contract debt"],
            },
            generator);

        ArchitectureBaselineComparisonEntry kept = generator.EntriesReceived!.Single(e => e.SourceType == "SrcKept");

        Assert.Multiple(() =>
        {
            Assert.That(kept.Reason, Is.EqualTo("reviewed reason"));
            Assert.That(kept.Issue, Is.EqualTo("PROJ-7"));
        });
    }

    [Test]
    public void Update_AddedEntry_UsesTheMappedReasonForItsFamily()
    {
        var generator = new FakeBaselineGenerator();
        RunLifecycleUpdate(
            new BaselineUpdateRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                BaselinePath = "unused-by-fakes.baseline.yml",
                Mode = "all",
                Reason = "flat debt",
                ReasonForFamily = ["strict=family debt"],
            },
            generator);

        Assert.That(
            generator.EntriesReceived!.Single(e => e.SourceType == "SrcNew").Reason,
            Is.EqualTo("family debt"));
    }

    [Test]
    public void Update_MalformedReasonMapping_FailsWithoutProducingADocument()
    {
        BaselineUpdateOutcome outcome = RunLifecycleUpdate(new BaselineUpdateRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
            ReasonForFamily = ["strict"],
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Yaml, Is.Null);
            Assert.That(outcome.Error, Does.Contain("--reason-for-family"));
        });
    }

    [Test]
    public void Update_CommentedBaseline_PreservesHeaderAndReportsUnanchorableComments()
    {
        var generator = new FakeBaselineGenerator { YamlToReturn = "version: 1\n" };
        BaselineUpdateOutcome preservable = RunLifecycleUpdate(
            new BaselineUpdateRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                BaselinePath = "unused-by-fakes.baseline.yml",
                Mode = "all",
            },
            generator,
            rawBaselineText: "# reviewed header\nversion: 1\nbaseline: {}\n");

        Assert.Multiple(() =>
        {
            Assert.That(preservable.CommentDiagnostic, Is.Null);
            Assert.That(preservable.Yaml, Does.StartWith("# reviewed header"));
            Assert.That(preservable.Yaml, Does.Contain("version: 1"));
        });

        BaselineUpdateOutcome unpreservable = RunLifecycleUpdate(
            new BaselineUpdateRequest
            {
                PolicyPath = "unused-by-fakes.arch.yml",
                BaselinePath = "unused-by-fakes.baseline.yml",
                Mode = "all",
            },
            new FakeBaselineGenerator(),
            rawBaselineText: "version: 1\n# a note next to an entry\nbaseline: {}\n");

        Assert.Multiple(() =>
        {
            // Classification still succeeds — only the write is refused, by the caller — so --dry-run
            // can print the proposal the reviewer needs.
            Assert.That(unpreservable.Succeeded, Is.True);
            Assert.That(unpreservable.CommentDiagnostic, Does.Contain("line(s) 2"));
            Assert.That(unpreservable.CommentDiagnostic, Does.Contain("--dry-run"));
        });
    }

    [Test]
    public void Prune_RemovesResolvedButRetainsAmbiguousEntries()
    {
        var generator = new FakeBaselineGenerator();
        BaselinePruneOutcome outcome = RunLifecyclePrune(generator);

        List<string> emitted = generator.EntriesReceived!.Select(e => e.SourceType).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(emitted, Does.Contain("SrcKept"));
            Assert.That(emitted, Does.Contain("SrcAmbiguous"));
            Assert.That(emitted, Does.Not.Contain("SrcStale"));
            Assert.That(
                outcome.Entries.Single(e => e.Entry.SourceType == "SrcStale").Lifecycle,
                Is.EqualTo(BaselineEntryLifecycle.Resolved));
            Assert.That(
                outcome.Entries.Single(e => e.Entry.SourceType == "SrcAmbiguous").Lifecycle,
                Is.EqualTo(BaselineEntryLifecycle.Ambiguous));
            Assert.That(outcome.RemovedEntries.Select(r => r.Entry.SourceType), Is.EqualTo(new[] { "SrcStale" }));
        });
    }

    [Test]
    public void Verify_AmbiguousEntry_ReportsOutOfSync()
    {
        (FakeRunnerSetupService runnerSetupService, FakeBaselineLoadingService loadingService) =
            CreateLifecycleCollaborators("version: 1\nbaseline: {}\n");

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), loadingService);

        BaselineVerifyOutcome outcome = applicationService.Verify(new BaselineVerifyRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(outcome.InSync, Is.False);
            Assert.That(outcome.Ambiguous.Select(e => e.SourceType), Is.EqualTo(new[] { "SrcAmbiguous" }));
        });
    }

    [Test]
    public void Diff_AmbiguousEntry_IsReportedSeparatelyFromFrozen()
    {
        (FakeRunnerSetupService runnerSetupService, FakeBaselineLoadingService loadingService) =
            CreateLifecycleCollaborators("version: 1\nbaseline: {}\n");

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            new FakeBaselineGenerator(), loadingService);

        BaselineDiffOutcome outcome = applicationService.Diff(new BaselineDiffRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Ambiguous.Select(e => e.SourceType), Is.EqualTo(new[] { "SrcAmbiguous" }));
            Assert.That(outcome.Frozen.Select(e => e.SourceType), Is.EqualTo(new[] { "SrcKept" }));
        });
    }

    private static BaselineUpdateOutcome RunLifecycleUpdate(
        BaselineUpdateRequest request,
        FakeBaselineGenerator? generator = null,
        string rawBaselineText = "version: 1\nbaseline: {}\n")
    {
        (FakeRunnerSetupService runnerSetupService, FakeBaselineLoadingService loadingService) =
            CreateLifecycleCollaborators(rawBaselineText);

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            generator ?? new FakeBaselineGenerator(), loadingService);

        return applicationService.Update(request);
    }

    private static BaselinePruneOutcome RunLifecyclePrune(FakeBaselineGenerator generator)
    {
        (FakeRunnerSetupService runnerSetupService, FakeBaselineLoadingService loadingService) =
            CreateLifecycleCollaborators("version: 1\nbaseline: {}\n");

        var applicationService = new ArchitectureBaselineApplicationService(
            runnerSetupService, new FakeContractHandlerRegistry(), new FakeContractExecutor(),
            generator, loadingService);

        return applicationService.Prune(new BaselinePruneRequest
        {
            PolicyPath = "unused-by-fakes.arch.yml",
            BaselinePath = "unused-by-fakes.baseline.yml",
            Mode = "all",
        });
    }

    /// <summary>
    /// One scenario exercising every disposition at once: a still-matching entry carrying reviewed
    /// metadata, an entry whose violation is gone, a legacy entry that now correlates to two
    /// candidates, and a current violation with no entry.
    /// </summary>
    private static (FakeRunnerSetupService RunnerSetup, FakeBaselineLoadingService Loading) CreateLifecycleCollaborators(
        string rawBaselineText)
    {
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Fake",
            Contracts = new ArchitectureContractGroups
            {
                Strict = new List<ArchitectureDependencyContract>
                {
                    new() { Id = "known-rule", Name = "known-rule", Source = "core" },
                },
            },
        };

        var runner = new FakeContractRunner(ArchitectureBaselineApplicationServiceHelper.CreateEmptySession(document))
        {
            BaselineCandidates = new List<ArchitectureBaselineCandidate>
            {
                new("strict", "known-rule", "SrcKept", "RefKept"),
                new("strict", "known-rule", "SrcAmbiguous", "RefAmbiguous"),
                new("strict", "known-rule", "SrcAmbiguous", "RefAmbiguous"),
                new("strict", "known-rule", "SrcNew", "RefNew"),
            },
        };

        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = runner,
        };

        var loadingService = new FakeBaselineLoadingService
        {
            RawTextToReturn = rawBaselineText,
            DocumentToReturn = new ArchitectureBaselineDocument
            {
                Version = 1,
                Baseline = new ArchitectureBaselineContractGroups
                {
                    Strict = new List<ArchitectureBaselineContractEntry>
                    {
                        new()
                        {
                            Id = "known-rule",
                            IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                            {
                                new()
                                {
                                    SourceType = "SrcKept",
                                    ForbiddenReference = "RefKept",
                                    Reason = "reviewed reason",
                                    Issue = "PROJ-7",
                                },
                                new() { SourceType = "SrcStale", ForbiddenReference = "RefStale", Reason = "gone" },
                                new()
                                {
                                    SourceType = "SrcAmbiguous",
                                    ForbiddenReference = "RefAmbiguous",
                                    Reason = "ambiguous legacy pair",
                                },
                            },
                        },
                    },
                },
            },
        };

        return (runnerSetupService, loadingService);
    }
}
