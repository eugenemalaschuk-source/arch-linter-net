using System.Threading;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class IgnoreMatcherTests
{
    [Test]
    public void IndexedMatcher_VersionedBaselineWithoutLiveIdentity_UsesTextAndGlobFallback()
    {
        var ignore = new ArchitectureIgnoredViolation
        {
            SourceType = "App\\Services\\*",
            ForbiddenReference = "System.*",
            IdentityVersion = ArchitectureViolationIdentity.CurrentVersion,
            ContractFamily = "strict",
            Kind = "dependency",
            SourceAssembly = "Some.Assembly",
        };
        ArchitectureIgnoreMatcherIndex index = ArchitectureIgnoreMatcherIndex.Build([ignore]);

        bool missingIdentityMatch = index.IsIgnored(
            "App/Services/Worker", "System.Object", null, null, null, out bool missingIdentityBaseline);
        bool liveIdentityMatch = index.IsIgnored(
            "App/Services/Worker", "System.Object", null, CreateIdentity(0), null, out bool liveIdentityBaseline);

        Assert.Multiple(() =>
        {
            Assert.That(missingIdentityMatch, Is.True, "A baseline entry retains its legacy fallback if no live identity exists.");
            Assert.That(missingIdentityBaseline, Is.False);
            Assert.That(liveIdentityMatch, Is.False, "A live identity makes a versioned baseline exact-only.");
            Assert.That(liveIdentityBaseline, Is.False);
        });
    }

    [Test]
    public void IndexedMatcher_InvalidStructuredWaiver_FailsClosedWithoutTextFallback()
    {
        var invalidWaiver = new ArchitectureIgnoredViolation
        {
            SourceType = "*",
            ForbiddenReference = "*",
            WaiverId = "ARCH-IGN-BAD",
            Target = new ArchitectureWaiverTarget { Fingerprint = ArchitectureWaiverTargetFingerprint.Create(CreateIdentity(0)) },
        };
        invalidWaiver.WaiverValidationError = "incomplete waiver";
        ArchitectureIgnoreMatcherIndex index = ArchitectureIgnoreMatcherIndex.Build([invalidWaiver]);

        bool ignored = index.IsIgnored("Source.Type", "Target.Type", null, CreateIdentity(0), null, out _);

        Assert.That(ignored, Is.False);
    }

    [Test]
    public void IndexedMatcher_TrackerGetsAllOrderedOverlappingMatches_WithoutTrackerUsesEarliestProvenance()
    {
        ArchitectureViolationIdentity identity = CreateIdentity(7);
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new() { SourceType = "*", ForbiddenReference = "Target.Type", IsFromLoadedBaseline = false },
            CreateVersionedIgnore(identity, isFromLoadedBaseline: true),
            CreateVersionedIgnore(identity, isFromLoadedBaseline: false),
            new() { SourceType = identity.SourceType, ForbiddenReference = "Target.Type", IsFromLoadedBaseline = true },
            new()
            {
                SourceType = "does-not-match",
                ForbiddenReference = "also-does-not-match",
                WaiverId = "ARCH-IGN-007",
                Target = new ArchitectureWaiverTarget { Fingerprint = ArchitectureWaiverTargetFingerprint.Create(identity) },
            },
        };
        ArchitectureIgnoreMatcherIndex index = ArchitectureIgnoreMatcherIndex.Build(ignored);
        string? fingerprint = index.CreateLiveWaiverFingerprint(identity);

        bool untrackedMatch = index.IsIgnored(identity.SourceType, "Target.Type", null, identity, fingerprint, out bool firstLoaded);
        var tracker = new ArchitectureIgnoreUsageTracker();
        bool trackedMatch = index.IsIgnored(identity.SourceType, "Target.Type", tracker, identity, fingerprint, out bool anyLoaded);

        Assert.Multiple(() =>
        {
            Assert.That(untrackedMatch, Is.True);
            Assert.That(firstLoaded, Is.False, "The first matching original entry is a policy entry.");
            Assert.That(trackedMatch, Is.True);
            Assert.That(anyLoaded, Is.True, "At least one later overlapping entry came from the loaded baseline.");
            Assert.That(tracker.MatchedIndexes, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }),
                "Both duplicate exact identities and the wildcard, literal, and waiver matches must be retained.");
        });
    }

    [TestCase(64, 16, 1, 16)]
    [TestCase(1024, 16, 1, 16)]
    [TestCase(1024, 256, 1, 256)]
    [TestCase(1024, 32, 8, 256)]
    public void IndexedMatcher_ExactIdentityWorkTracksIPlusFPlusK(
        int ignoreCount,
        int findingCount,
        int duplicatesPerFinding,
        int expectedMatches)
    {
        var ignored = new List<ArchitectureIgnoredViolation>(ignoreCount);
        for (int index = 0; index < ignoreCount; index++)
        {
            int findingIndex = index < expectedMatches
                ? index / duplicatesPerFinding
                : findingCount + index;
            ignored.Add(CreateVersionedIgnore(CreateIdentity(findingIndex), isFromLoadedBaseline: index % 2 == 0));
        }

        var work = new ArchitectureIgnoreMatcherWorkCounter();
        ArchitectureIgnoreMatcherIndex matcherIndex = ArchitectureIgnoreMatcherIndex.Build(ignored, workCounter: work);
        var tracker = new ArchitectureIgnoreUsageTracker();

        for (int finding = 0; finding < findingCount; finding++)
        {
            ArchitectureViolationIdentity identity = CreateIdentity(finding);
            Assert.That(matcherIndex.IsIgnored(
                identity.SourceType,
                $"Target.Type.{finding}",
                tracker,
                identity,
                matcherIndex.CreateLiveWaiverFingerprint(identity),
                out _), Is.True);
        }

        Assert.Multiple(() =>
        {
            Assert.That(work.IndexEntriesProcessed, Is.EqualTo(ignoreCount));
            Assert.That(work.ExactLookupProbes, Is.EqualTo(findingCount));
            Assert.That(work.ExactCandidateMatches, Is.EqualTo(expectedMatches));
            Assert.That(work.MatchedOriginalIndices, Is.EqualTo(expectedMatches));
            Assert.That(work.WildcardEntriesEvaluated, Is.Zero);
            Assert.That(tracker.MatchedIndexes, Has.Count.EqualTo(expectedMatches));
        });
    }

    [Test]
    public void IndexedMatcher_LiteralPairLookupDoesNotScanEntriesPerFinding()
    {
        const int IgnoreCount = 256;
        const int FindingCount = 64;
        var ignored = new List<ArchitectureIgnoredViolation>(IgnoreCount);
        for (int index = 0; index < IgnoreCount; index++)
        {
            ignored.Add(new ArchitectureIgnoredViolation
            {
                SourceType = index < FindingCount ? $"Source.Type.{index}" : $"Other.Source.{index}",
                ForbiddenReference = index < FindingCount ? $"Target.Type.{index}" : $"Other.Target.{index}",
            });
        }

        var work = new ArchitectureIgnoreMatcherWorkCounter();
        ArchitectureIgnoreMatcherIndex matcherIndex = ArchitectureIgnoreMatcherIndex.Build(ignored, workCounter: work);
        var tracker = new ArchitectureIgnoreUsageTracker();
        for (int finding = 0; finding < FindingCount; finding++)
        {
            Assert.That(matcherIndex.IsIgnored(
                $"Source.Type.{finding}", $"Target.Type.{finding}", tracker, null, null, out _), Is.True);
        }

        Assert.Multiple(() =>
        {
            Assert.That(work.IndexEntriesProcessed, Is.EqualTo(IgnoreCount));
            Assert.That(work.ExactLookupProbes, Is.EqualTo(FindingCount));
            Assert.That(work.ExactCandidateMatches, Is.EqualTo(FindingCount));
            Assert.That(work.WildcardEntriesEvaluated, Is.Zero);
            Assert.That(tracker.MatchedIndexes, Has.Count.EqualTo(FindingCount));
        });
    }

    [Test]
    public void IndexedMatcher_CancellationAppliesToPreparationAndWildcardQueries()
    {
        using var cancelledDuringBuild = new CancellationTokenSource();
        cancelledDuringBuild.Cancel();
        Assert.Throws<OperationCanceledException>(() => ArchitectureIgnoreMatcherIndex.Build(
            [new ArchitectureIgnoredViolation { SourceType = "*", ForbiddenReference = "*" }],
            cancelledDuringBuild.Token));

        using var cancelledDuringQuery = new CancellationTokenSource();
        ArchitectureIgnoreMatcherIndex index = ArchitectureIgnoreMatcherIndex.Build(
            [new ArchitectureIgnoredViolation { SourceType = "*", ForbiddenReference = "*" }],
            cancelledDuringQuery.Token);
        cancelledDuringQuery.Cancel();

        Assert.Throws<OperationCanceledException>(() => index.IsIgnored("Source", "Target", null, null, null, out _));
    }

    [Test]
    public void IndexedMatcher_DeterministicRandomizedCasesMatchTheOrderedReferenceMatcher()
    {
        var random = new Random(1127);
        string[] sourceValues = ["Source.Type", "Source.Other", "Source/A", "Folder/X", "Other/Path"];
        string[] forbiddenValues = ["Target", "Target.Kind", "Namespace.A.Thing", "X/Y"];
        string[] sourcePatterns = ["Source.Type", "Source.*", "Source.?ther", "*", @"Folder\*", "", " "];
        string[] forbiddenPatterns = ["Target", "Target.*", "Namespace.**.Thing", "X?Y", "*", ""];

        for (int sample = 0; sample < 120; sample++)
        {
            ArchitectureViolationIdentity? liveIdentity = random.Next(4) == 0
                ? null
                : CreateIdentity(random.Next(8));
            var ignored = new List<ArchitectureIgnoredViolation>();
            int entryCount = random.Next(0, 40);
            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                string sourcePattern = sourcePatterns[random.Next(sourcePatterns.Length)];
                string forbiddenPattern = forbiddenPatterns[random.Next(forbiddenPatterns.Length)];
                switch (random.Next(6))
                {
                    case 0:
                    case 1:
                        ignored.Add(new ArchitectureIgnoredViolation
                        {
                            SourceType = sourcePattern,
                            ForbiddenReference = forbiddenPattern,
                            IsFromLoadedBaseline = random.Next(2) == 0,
                        });
                        break;
                    case 2:
                        ignored.Add(new ArchitectureIgnoredViolation
                        {
                            SourceType = sourcePattern,
                            ForbiddenReference = forbiddenPattern,
                            IdentityVersion = 1,
                            IsFromLoadedBaseline = random.Next(2) == 0,
                        });
                        break;
                    case 3:
                        ArchitectureViolationIdentity baselineIdentity = CreateIdentity(random.Next(8));
                        ignored.Add(CreateVersionedIgnore(baselineIdentity, random.Next(2) == 0, sourcePattern, forbiddenPattern));
                        break;
                    case 4:
                        ArchitectureViolationIdentity waiverIdentity = CreateIdentity(random.Next(8));
                        ignored.Add(new ArchitectureIgnoredViolation
                        {
                            SourceType = sourcePattern,
                            ForbiddenReference = forbiddenPattern,
                            WaiverId = $"waiver-{entryIndex}",
                            Target = new ArchitectureWaiverTarget
                            {
                                Fingerprint = ArchitectureWaiverTargetFingerprint.Create(waiverIdentity),
                            },
                            IsFromLoadedBaseline = random.Next(2) == 0,
                        });
                        break;
                    default:
                        var invalidWaiver = new ArchitectureIgnoredViolation
                        {
                            SourceType = sourcePattern,
                            ForbiddenReference = forbiddenPattern,
                            WaiverId = $"invalid-{entryIndex}",
                            Target = new ArchitectureWaiverTarget
                            {
                                Fingerprint = ArchitectureWaiverTargetFingerprint.Create(CreateIdentity(random.Next(8))),
                            },
                            IsFromLoadedBaseline = random.Next(2) == 0,
                        };
                        invalidWaiver.WaiverValidationError = "invalid fixture waiver";
                        ignored.Add(invalidWaiver);
                        break;
                }
            }

            string sourceType = sourceValues[random.Next(sourceValues.Length)];
            string forbiddenReference = forbiddenValues[random.Next(forbiddenValues.Length)];
            ArchitectureIgnoreMatcherIndex index = ArchitectureIgnoreMatcherIndex.Build(ignored);
            string? fingerprint = index.CreateLiveWaiverFingerprint(liveIdentity);

            bool expectedUntracked = ArchitectureIgnoreMatcherTestReference.IsIgnored(
                sourceType, forbiddenReference, ignored, null, liveIdentity, out bool expectedUntrackedBaseline);
            bool actualUntracked = index.IsIgnored(
                sourceType, forbiddenReference, null, liveIdentity, fingerprint, out bool actualUntrackedBaseline);

            var expectedTracker = new ArchitectureIgnoreUsageTracker();
            bool expectedTracked = ArchitectureIgnoreMatcherTestReference.IsIgnored(
                sourceType, forbiddenReference, ignored, expectedTracker, liveIdentity, out bool expectedTrackedBaseline);
            var actualTracker = new ArchitectureIgnoreUsageTracker();
            bool actualTracked = index.IsIgnored(
                sourceType, forbiddenReference, actualTracker, liveIdentity, fingerprint, out bool actualTrackedBaseline);

            Assert.Multiple(() =>
            {
                Assert.That(actualUntracked, Is.EqualTo(expectedUntracked), $"Suppression mismatch in sample {sample}.");
                Assert.That(actualUntrackedBaseline, Is.EqualTo(expectedUntrackedBaseline), $"First-match provenance mismatch in sample {sample}.");
                Assert.That(actualTracked, Is.EqualTo(expectedTracked), $"Tracked suppression mismatch in sample {sample}.");
                Assert.That(actualTrackedBaseline, Is.EqualTo(expectedTrackedBaseline), $"Tracked provenance mismatch in sample {sample}.");
                Assert.That(actualTracker.MatchedIndexes, Is.EquivalentTo(expectedTracker.MatchedIndexes), $"Matched indices mismatch in sample {sample}.");
            });
        }
    }

    private static ArchitectureViolationIdentity CreateIdentity(int index) => new(
        ArchitectureViolationIdentity.CurrentVersion,
        "strict",
        "dependency",
        $"contract-{index % 2}",
        $"Source.Assembly.{index}",
        $"Source.Type.{index}",
        $"Source.Type.{index}.Run",
        $"Target.Assembly.{index}",
        $"Target.Type.{index}",
        $"Target.Type.{index}.Create",
        index,
        index % 2 == 0 ? "Debug" : null);

    private static ArchitectureIgnoredViolation CreateVersionedIgnore(
        ArchitectureViolationIdentity identity,
        bool isFromLoadedBaseline,
        string? sourceType = null,
        string forbiddenReference = "Target.Type") => new()
        {
            SourceType = sourceType ?? identity.SourceType,
            ForbiddenReference = forbiddenReference,
            IdentityVersion = ArchitectureViolationIdentity.CurrentVersion,
            ContractFamily = identity.ContractFamily,
            Kind = identity.Kind,
            SourceAssembly = identity.SourceAssembly,
            SourceMember = identity.SourceMember,
            TargetAssembly = identity.TargetAssembly,
            TargetType = identity.TargetType,
            TargetMember = identity.TargetMember,
            Occurrence = identity.Occurrence,
            Configuration = identity.Configuration,
            IsFromLoadedBaseline = isFromLoadedBaseline,
        };
}
