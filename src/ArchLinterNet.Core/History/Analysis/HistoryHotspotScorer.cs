using System.Numerics;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Git;

namespace ArchLinterNet.Core.History.Analysis;

// Scores already canonicalized evidence only. Git/object, metadata, TaskKey, lifetime, and rename
// decisions remain exclusively owned by ingestion; this layer only derives cohort-local metrics.
internal sealed class HistoryHotspotScorer
{
    private const decimal Scale = 1_000_000_000m;
    private static readonly BigInteger IntegerScale = new(1_000_000_000);

    public HistoryHotspotAnalysis Score(HistoryIngestionResult result, HistoryAnalysisConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(configuration);

        Dictionary<string, CommitEvidence> commits = result.Commits.ToDictionary(
            static evidence => evidence.Commit.Id.Hex,
            StringComparer.Ordinal);
        var classifier = new HistoryPathClassifier(configuration);
        List<Candidate> candidates = [];
        foreach (LogicalFile file in result.LogicalFiles)
        {
            HistoryPathClassification classification = classifier.Classify(file.CanonicalPath);
            if (!classification.IsIgnored)
            {
                candidates.Add(CreateCandidate(file, classification.Category, commits));
            }
        }

        HotspotWeights weights = Weights(configuration.Weights.Hotspot);
        var groups = new List<HotspotCategoryGroup>();
        foreach (IGrouping<HistoryPathCategory, Candidate> cohort in candidates.GroupBy(static candidate => candidate.Category)
                     .OrderBy(static cohort => cohort.Key))
        {
            List<HotspotFinding> findings = ScoreCohort(cohort.ToArray(), weights);
            findings.Sort(CompareFindings);
            groups.Add(new HotspotCategoryGroup(cohort.Key, findings));
        }

        return new HistoryHotspotAnalysis(groups);
    }

    private static Candidate CreateCandidate(
        LogicalFile file,
        HistoryPathCategory category,
        IReadOnlyDictionary<string, CommitEvidence> commits)
    {
        var taskKeys = new HashSet<Tasks.TaskKey>();
        var authors = new HashSet<string>(StringComparer.Ordinal);
        BigInteger? earliest = null;
        BigInteger? latest = null;
        foreach (FileEvent fileEvent in file.Events)
        {
            if (!commits.TryGetValue(fileEvent.CommitId, out CommitEvidence? evidence))
            {
                throw new InvalidOperationException($"Canonical file event references unknown commit '{fileEvent.CommitId}'.");
            }

            taskKeys.UnionWith(evidence.TaskKeys);
            authors.Add(evidence.CanonicalAuthor);
            BigInteger epoch = evidence.Commit.CommitterEpochSecond;
            earliest = earliest is null || epoch < earliest ? epoch : earliest;
            latest = latest is null || epoch > latest ? epoch : latest;
        }

        BigInteger span = earliest is null ? BigInteger.Zero : latest!.Value - earliest.Value;
        LineCountStatus[] statuses = file.Events.Select(static item => item.LineCountStatus).Distinct().Order().ToArray();
        return new Candidate(
            file.CanonicalPath,
            category,
            new HotspotRawEvidence(file.CommitCount, file.Churn, taskKeys.Count, authors.Count, span, statuses));
    }

    private static List<HotspotFinding> ScoreCohort(IReadOnlyList<Candidate> candidates, HotspotWeights weights)
    {
        int maxCommitCount = candidates.Max(static candidate => candidate.RawEvidence.CommitCount);
        long maxChurn = candidates.Max(static candidate => candidate.RawEvidence.Churn);
        int maxTaskSpread = candidates.Max(static candidate => candidate.RawEvidence.TaskSpread);
        int maxAuthorSpread = candidates.Max(static candidate => candidate.RawEvidence.AuthorSpread);
        BigInteger maxTemporalSpan = candidates.Max(static candidate => candidate.RawEvidence.TemporalSpanSeconds);
        var findings = new List<HotspotFinding>(candidates.Count);
        foreach (Candidate candidate in candidates)
        {
            HotspotRawEvidence raw = candidate.RawEvidence;
            var components = new HotspotComponents(
                QuantizedRatio(raw.CommitCount, maxCommitCount),
                QuantizedLogRatio(raw.Churn, maxChurn),
                QuantizedRatio(raw.TaskSpread, maxTaskSpread),
                QuantizedRatio(raw.AuthorSpread, maxAuthorSpread),
                QuantizedRatio(raw.TemporalSpanSeconds, maxTemporalSpan));
            decimal score = Quantize(
                (weights.Commit * components.Commit) +
                (weights.Churn * components.Churn) +
                (weights.Task * components.Task) +
                (weights.Author * components.Author) +
                (weights.Temporal * components.Temporal));
            findings.Add(new HotspotFinding(candidate.CanonicalPath, candidate.Category, raw, components, weights, score));
        }

        return findings;
    }

    private static HotspotWeights Weights(HistoryHotspotWeightProfile profile) => new(
        profile.Commit, profile.Churn, profile.Task, profile.Author, profile.Temporal);

    private static decimal QuantizedRatio(int value, int maximum) => QuantizedRatio(new BigInteger(value), new BigInteger(maximum));

    private static decimal QuantizedRatio(BigInteger value, BigInteger maximum)
    {
        if (maximum.IsZero)
        {
            return 0m;
        }

        BigInteger scaledNumerator = value * IntegerScale;
        BigInteger quotient = BigInteger.DivRem(scaledNumerator, maximum, out BigInteger remainder);
        int comparison = (remainder * 2).CompareTo(maximum);
        if (comparison > 0 || (comparison == 0 && !quotient.IsEven))
        {
            quotient++;
        }

        return (decimal)quotient / Scale;
    }

    private static decimal QuantizedLogRatio(long value, long maximum)
    {
        if (maximum == 0)
        {
            return 0m;
        }

        double ratio = Math.Log(1d + value) / Math.Log(1d + maximum);
        return Quantize((decimal)ratio);
    }

    private static decimal Quantize(decimal value) => Math.Round(value, 9, MidpointRounding.ToEven);

    private static int CompareFindings(HotspotFinding left, HotspotFinding right)
    {
        int comparison = right.Score.CompareTo(left.Score);
        comparison = comparison != 0 ? comparison : right.RawEvidence.TaskSpread.CompareTo(left.RawEvidence.TaskSpread);
        comparison = comparison != 0 ? comparison : right.RawEvidence.Churn.CompareTo(left.RawEvidence.Churn);
        comparison = comparison != 0 ? comparison : right.RawEvidence.CommitCount.CompareTo(left.RawEvidence.CommitCount);
        return comparison != 0 ? comparison : GitPathDecoder.CompareScalarValue(left.CanonicalPath, right.CanonicalPath);
    }

    private sealed class Candidate(string canonicalPath, HistoryPathCategory category, HotspotRawEvidence rawEvidence)
    {
        public string CanonicalPath { get; } = canonicalPath;

        public HistoryPathCategory Category { get; } = category;

        public HotspotRawEvidence RawEvidence { get; } = rawEvidence;
    }
}
