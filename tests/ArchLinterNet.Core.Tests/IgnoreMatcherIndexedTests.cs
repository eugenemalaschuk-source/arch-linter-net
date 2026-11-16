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
        ArchitectureIgnoredViolation[] ignored = Enumerable.Repeat(ignore, 9).ToArray();
        ArchitectureIgnoreMatcherWorkCounter work = new();
        ArchitectureIgnoreMatcherIndex index = ArchitectureIgnoreMatcherIndex.Build(ignored, workCounter: work);

        bool missingIdentityMatch = index.IsIgnored(
            "App/Services/Worker", "System.Object", null, null, null, out bool missingIdentityBaseline);
        Assert.That(work.VersionedIdentityEntriesIndexed, Is.Zero,
            "A missing live identity must not build the exact identity index.");
        Assert.That(work.VersionedFallbackEntriesProcessed, Is.Zero,
            "A short fallback query must not build the fallback index.");
        bool liveIdentityMatch = index.IsIgnored(
            "App/Services/Worker", "System.Object", null, CreateIdentity(0), null, out bool liveIdentityBaseline);

        Assert.Multiple(() =>
        {
            Assert.That(missingIdentityMatch, Is.True, "A baseline entry retains its legacy fallback if no live identity exists.");
            Assert.That(missingIdentityBaseline, Is.False);
            Assert.That(liveIdentityMatch, Is.False, "A live identity makes a versioned baseline exact-only.");
            Assert.That(liveIdentityBaseline, Is.False);
            Assert.That(work.VersionedIdentityEntriesIndexed, Is.EqualTo(9),
                "The exact identity index is built when a live identity first needs it.");
        });
    }

    [Test]
    public void IndexedMatcher_SmallLiveIdentityListUsesOrderedMatchingWithoutBuildingIndex()
    {
        ArchitectureViolationIdentity identity = CreateIdentity(0);
        ArchitectureIgnoredViolation ignore = CreateVersionedIgnore(identity, isFromLoadedBaseline: true);
        var work = new ArchitectureIgnoreMatcherWorkCounter();
        var index = ArchitectureIgnoreMatcherIndex.Build([ignore], workCounter: work);

        string? fingerprint = index.CreateLiveWaiverFingerprint(identity);
        bool matched = index.IsIgnored(
            identity.SourceType,
            "Target.Type.0",
            null,
            identity,
            fingerprint,
            out bool matchedByLoadedBaseline);

        Assert.Multiple(() =>
        {
            Assert.That(matched, Is.True);
            Assert.That(matchedByLoadedBaseline, Is.True);
            Assert.That(work.IndexEntriesProcessed, Is.Zero,
                "A small ignore list should avoid allocating exact lookup dictionaries.");
            Assert.That(work.VersionedIdentityEntriesIndexed, Is.Zero);
        });
    }

    [Test]
    public void IndexedMatcher_VersionedFallbackIndexIsDeferredAndBuiltOnlyOnce()
    {
        ArchitectureViolationIdentity identity = CreateIdentity(0);
        ArchitectureIgnoredViolation ignore = CreateVersionedIgnore(
            identity,
            isFromLoadedBaseline: false,
            sourceType: "Fallback.Source",
            forbiddenReference: "Fallback.Target");
        var work = new ArchitectureIgnoreMatcherWorkCounter();
        var index = ArchitectureIgnoreMatcherIndex.Build([ignore], workCounter: work);

        bool liveIdentityMatch = index.IsIgnored(
            "Fallback.Source", "Fallback.Target", null, CreateIdentity(1), null, out _);

        Assert.That(liveIdentityMatch, Is.False);
        Assert.That(work.VersionedFallbackEntriesProcessed, Is.Zero,
            "A live identity must not materialize a fallback index that cannot be consulted.");

        for (int query = 0; query < 4; query++)
        {
            bool fallbackMatch = index.IsIgnored(
                "Fallback.Source", "Fallback.Target", null, null, null, out _);
            Assert.That(fallbackMatch, Is.True);
            Assert.That(work.VersionedFallbackEntriesProcessed, Is.Zero,
                "A few early matches should keep using the compact ordered list.");
        }

        bool indexedFallbackMatch = index.IsIgnored(
            "Fallback.Source", "Fallback.Target", null, null, null, out _);
        bool reusedFallbackIndexMatch = index.IsIgnored(
            "Fallback.Source", "Fallback.Target", null, null, null, out _);

        Assert.Multiple(() =>
        {
            Assert.That(indexedFallbackMatch, Is.True);
            Assert.That(reusedFallbackIndexMatch, Is.True);
            Assert.That(work.IndexEntriesProcessed, Is.Zero,
                "The small live-identity path must not prepare the separate live index.");
            Assert.That(work.VersionedFallbackEntriesProcessed, Is.EqualTo(1),
                "The immutable fallback lookup should be initialized after the scan threshold and reused.");
            Assert.That(work.MissingIdentityEntriesScanned, Is.EqualTo(4));
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
            WaiverValidationError = "incomplete waiver",
        };
        var index = ArchitectureIgnoreMatcherIndex.Build([invalidWaiver]);

        bool ignored = index.IsIgnored("Source.Type", "Target.Type", null, CreateIdentity(0), null, out _);

        Assert.That(ignored, Is.False);
    }

    [Test]
    public void IndexedMatcher_NullStructuredFingerprint_FailsClosedLikeOrderedMatcher()
    {
        ArchitectureIgnoredViolation invalidWaiver = new()
        {
            SourceType = "*",
            ForbiddenReference = "*",
            WaiverId = "ARCH-IGN-NULL-FINGERPRINT",
            Target = new ArchitectureWaiverTarget { Fingerprint = null! },
        };
        ArchitectureViolationIdentity liveIdentity = CreateIdentity(0);

        bool orderedMatch = ArchitectureIgnoreMatcherTestReference.IsIgnored(
            "Source.Type", "Target.Type", [invalidWaiver], null, liveIdentity, out _);
        ArchitectureIgnoreMatcherIndex index = ArchitectureIgnoreMatcherIndex.Build([invalidWaiver]);
        string? fingerprint = index.CreateLiveWaiverFingerprint(liveIdentity);
        bool indexedMatch = index.IsIgnored(
            "Source.Type", "Target.Type", null, liveIdentity, fingerprint, out _);

        Assert.That(indexedMatch, Is.EqualTo(orderedMatch));
        Assert.That(indexedMatch, Is.False);
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
        var index = ArchitectureIgnoreMatcherIndex.Build(ignored);
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
            Assert.That(tracker.MatchedIndexes, Is.EquivalentTo([0, 1, 2, 3, 4]),
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
        var matcherIndex = ArchitectureIgnoreMatcherIndex.Build(ignored, workCounter: work);
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
            Assert.That(work.VersionedIdentityEntriesIndexed, Is.EqualTo(ignoreCount));
            Assert.That(work.ExactLookupProbes + work.OrderedLiveQueries, Is.EqualTo(findingCount));
            Assert.That(work.ExactCandidateMatches + work.OrderedLiveMatches, Is.EqualTo(expectedMatches));
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
        var matcherIndex = ArchitectureIgnoreMatcherIndex.Build(ignored, workCounter: work);
        var tracker = new ArchitectureIgnoreUsageTracker();
        ArchitectureViolationIdentity liveIdentity = CreateIdentity(0);
        for (int finding = 0; finding < FindingCount; finding++)
        {
            Assert.That(matcherIndex.IsIgnored(
                $"Source.Type.{finding}", $"Target.Type.{finding}", tracker, liveIdentity, null, out _), Is.True);
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
        bool buildCancelled = false;
        try
        {
            _ = ArchitectureIgnoreMatcherIndex.Build(
                [new ArchitectureIgnoredViolation { SourceType = "*", ForbiddenReference = "*" }],
                cancelledDuringBuild.Token);
        }
        catch (OperationCanceledException)
        {
            buildCancelled = true;
        }

        Assert.That(buildCancelled, Is.True);

        using var cancelledDuringQuery = new CancellationTokenSource();
        var index = ArchitectureIgnoreMatcherIndex.Build(
            [new ArchitectureIgnoredViolation { SourceType = "*", ForbiddenReference = "*" }],
            cancelledDuringQuery.Token);
        cancelledDuringQuery.Cancel();

        bool queryCancelled = false;
        try
        {
            index.IsIgnored("Source", "Target", null, null, null, out _);
        }
        catch (OperationCanceledException)
        {
            queryCancelled = true;
        }

        Assert.That(queryCancelled, Is.True);
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
                            WaiverValidationError = "invalid fixture waiver",
                        };
                        ignored.Add(invalidWaiver);
                        break;
                }
            }

            string sourceType = sourceValues[random.Next(sourceValues.Length)];
            string forbiddenReference = forbiddenValues[random.Next(forbiddenValues.Length)];
            var index = ArchitectureIgnoreMatcherIndex.Build(ignored);
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

            Assert.That(actualUntracked, Is.EqualTo(expectedUntracked), $"Suppression mismatch in sample {sample}.");
            Assert.That(actualUntrackedBaseline, Is.EqualTo(expectedUntrackedBaseline), $"First-match provenance mismatch in sample {sample}.");
            Assert.That(actualTracked, Is.EqualTo(expectedTracked), $"Tracked suppression mismatch in sample {sample}.");
            Assert.That(actualTrackedBaseline, Is.EqualTo(expectedTrackedBaseline), $"Tracked provenance mismatch in sample {sample}.");
            Assert.That(actualTracker.MatchedIndexes, Is.EquivalentTo(expectedTracker.MatchedIndexes), $"Matched indices mismatch in sample {sample}.");
        }
    }

    private static ArchitectureViolationIdentity CreateIdentity(int index)
    {
        return new ArchitectureViolationIdentity(
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
    }

    private static ArchitectureIgnoredViolation CreateVersionedIgnore(
        ArchitectureViolationIdentity identity,
        bool isFromLoadedBaseline,
        string? sourceType = null,
        string forbiddenReference = "Target.Type")
    {
        return new ArchitectureIgnoredViolation
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
}
