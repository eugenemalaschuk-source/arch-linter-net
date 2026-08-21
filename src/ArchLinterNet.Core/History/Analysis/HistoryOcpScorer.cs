using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.History.Configuration;
using ArchLinterNet.Core.History.Git;
using ArchLinterNet.Core.History.Tasks;

namespace ArchLinterNet.Core.History.Analysis;

internal sealed class HistoryOcpScorer
{
    private const decimal Scale = 1_000_000_000m;
    private static readonly HashSet<string> _roleTokens = new(StringComparer.Ordinal)
    {
        "dispatcher", "registry", "handler", "loader", "session", "options", "configuration",
        "command", "diagnostic", "mapper", "dto", "model", "service", "orchestrator",
    };

    public HistoryOcpAnalysis Score(
        HistoryBottleneckAnalysis bottleneckAnalysis,
        CoChangeGraph coChangeGraph,
        HistoryAnalysisConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(bottleneckAnalysis);
        ArgumentNullException.ThrowIfNull(coChangeGraph);
        ArgumentNullException.ThrowIfNull(configuration);

        OcpWeights weights = Weights(configuration.Weights.Ocp);
        IReadOnlyDictionary<string, LogicalFile> filesByPath = coChangeGraph.Vertices.ToDictionary(
            static vertex => vertex.CanonicalPath,
            static vertex => vertex.File,
            StringComparer.Ordinal);
        List<Candidate> candidates = bottleneckAnalysis.Findings
            .Select(finding => CreateCandidate(finding, filesByPath[finding.CanonicalPath]))
            .ToList();
        List<HistoryOcpCategoryGroup> groups = [];
        foreach (IGrouping<HistoryPathCategory, Candidate> category in candidates.GroupBy(static candidate => candidate.Bottleneck.Category)
                     .OrderBy(static group => group.Key))
        {
            List<HistoryOcpFinding> findings = ScoreCategory(category.ToArray(), weights);
            findings.Sort(CompareFindings);
            groups.Add(new HistoryOcpCategoryGroup(category.Key, findings));
        }

        return new HistoryOcpAnalysis(groups);
    }

    internal static IReadOnlyList<string> RoleTokens(string canonicalPath)
    {
        string stem = FilenameStem(canonicalPath);
        List<string> tokens = [];
        int start = 0;
        while (start < stem.Length)
        {
            while (start < stem.Length && !IsAsciiAlphaNumeric(stem[start]))
            {
                start++;
            }

            if (start == stem.Length)
            {
                break;
            }

            int end = start + 1;
            while (end < stem.Length && IsAsciiAlphaNumeric(stem[end]))
            {
                if (ShouldSplit(stem, end))
                {
                    break;
                }

                end++;
            }

            string token = ToAsciiLower(stem[start..end]);
            if (_roleTokens.Contains(token))
            {
                tokens.Add(token);
            }

            start = end;
        }

        tokens.Sort(GitPathDecoder.CompareScalarValue);
        return tokens.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static List<HistoryOcpFinding> ScoreCategory(IReadOnlyList<Candidate> candidates, OcpWeights weights)
    {
        int maximumTasks = candidates.Max(static candidate => candidate.Raw.IndependentTaskSpread);
        int maximumRepeated = candidates.Max(static candidate => candidate.Raw.RepeatedEditTotal);
        List<HistoryOcpFinding> findings = [];
        foreach (Candidate candidate in candidates)
        {
            OcpComponents components = new(
                QuantizedRatio(candidate.Raw.IndependentTaskSpread, maximumTasks),
                candidate.Bottleneck.Components.Centrality,
                QuantizedRatio(candidate.Raw.RepeatedEditTotal, maximumRepeated),
                candidate.Raw.RoleHint);
            decimal score = Quantize(
                (weights.IndependentTask * components.IndependentTask) +
                (weights.Centrality * components.Centrality) +
                (weights.RepeatedEdit * components.RepeatedEdit) +
                (weights.RoleHint * components.RoleHint));
            findings.Add(new HistoryOcpFinding(
                candidate.Bottleneck.CanonicalPath,
                candidate.Bottleneck.Aliases,
                candidate.Bottleneck.Category,
                candidate.Raw,
                components,
                weights,
                score));
        }

        return findings;
    }

    private static Candidate CreateCandidate(HistoryBottleneckFinding bottleneck, LogicalFile file)
    {
        Dictionary<TaskKey, HashSet<string>> qualifyingByTask = [];
        foreach (BottleneckTaskPair pair in bottleneck.RawEvidence.IndependentTaskPairs)
        {
            AddQualifying(qualifyingByTask, pair.First, pair.FirstExclusiveCommitIds);
            AddQualifying(qualifyingByTask, pair.Second, pair.SecondExclusiveCommitIds);
        }

        OcpTaskRepeatedEdit[] repeatedEdits = qualifyingByTask
            .OrderBy(static item => item.Key)
            .Select(static item => new OcpTaskRepeatedEdit(item.Key, item.Value.OrderBy(static id => id, StringComparer.Ordinal).ToArray()))
            .ToArray();
        int repeatedTotal = repeatedEdits.Sum(static repeated => repeated.RepeatedEditCount);
        IReadOnlyList<string> roleTokens = RoleTokens(bottleneck.CanonicalPath);
        return new Candidate(
            bottleneck,
            new OcpRawEvidence(
                bottleneck.RawEvidence.IndependentTaskSpread,
                bottleneck.RawEvidence.TaskKeys.Count,
                file.Churn,
                file.CommitCount,
                bottleneck.RawEvidence.IncidentCommitDegree,
                bottleneck.RawEvidence.IncidentTaskDegree,
                repeatedTotal,
                roleTokens.Count == 0 ? 0m : 1m,
                bottleneck.RawEvidence.TaskKeys,
                bottleneck.RawEvidence.IndependentTaskPairs,
                repeatedEdits,
                roleTokens));
    }

    private static void AddQualifying(Dictionary<TaskKey, HashSet<string>> byTask, TaskKey task, IReadOnlyList<string> commitIds)
    {
        if (!byTask.TryGetValue(task, out HashSet<string>? qualifying))
        {
            qualifying = new HashSet<string>(StringComparer.Ordinal);
            byTask.Add(task, qualifying);
        }

        qualifying.UnionWith(commitIds);
    }

    private static OcpWeights Weights(HistoryOcpWeightProfile profile) => new(
        profile.IndependentTask,
        profile.Centrality,
        profile.RepeatedEdit,
        profile.RoleHint);

    private static decimal QuantizedRatio(int value, int maximum)
        => maximum == 0 ? 0m : Quantize((decimal)value / maximum);

    private static decimal Quantize(decimal value) => decimal.Round(value, 9, MidpointRounding.ToEven);

    private static int CompareFindings(HistoryOcpFinding left, HistoryOcpFinding right)
    {
        int byScore = right.Score.CompareTo(left.Score);
        if (byScore != 0)
        {
            return byScore;
        }

        int byTaskSpread = right.RawEvidence.OrdinaryTaskKeySpread.CompareTo(left.RawEvidence.OrdinaryTaskKeySpread);
        if (byTaskSpread != 0)
        {
            return byTaskSpread;
        }

        int byChurn = right.RawEvidence.Churn.CompareTo(left.RawEvidence.Churn);
        if (byChurn != 0)
        {
            return byChurn;
        }

        int byCommitCount = right.RawEvidence.CommitCount.CompareTo(left.RawEvidence.CommitCount);
        return byCommitCount != 0 ? byCommitCount : GitPathDecoder.CompareScalarValue(left.CanonicalPath, right.CanonicalPath);
    }

    private static string FilenameStem(string path)
    {
        int slash = path.LastIndexOf('/');
        int start = slash + 1;
        int dot = path.LastIndexOf('.');
        int end = dot > start ? dot : path.Length;
        return path[start..end];
    }

    private static bool ShouldSplit(string text, int index)
    {
        char previous = text[index - 1];
        char current = text[index];
        bool letterDigit = (IsAsciiLetter(previous) && IsAsciiDigit(current)) || (IsAsciiDigit(previous) && IsAsciiLetter(current));
        if (letterDigit || (IsAsciiLower(previous) && IsAsciiUpper(current)))
        {
            return true;
        }

        return IsAsciiUpper(previous) && IsAsciiUpper(current) && index + 1 < text.Length && IsAsciiLower(text[index + 1]);
    }

    private static bool IsAsciiAlphaNumeric(char value) => IsAsciiLetter(value) || IsAsciiDigit(value);

    private static bool IsAsciiLetter(char value) => IsAsciiLower(value) || IsAsciiUpper(value);

    private static bool IsAsciiLower(char value) => value is >= 'a' and <= 'z';

    private static bool IsAsciiUpper(char value) => value is >= 'A' and <= 'Z';

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static string ToAsciiLower(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            buffer[index] = IsAsciiUpper(character) ? (char)(character + ('a' - 'A')) : character;
        }

        return new string(buffer);
    }

    private sealed class Candidate(HistoryBottleneckFinding bottleneck, OcpRawEvidence raw)
    {
        public HistoryBottleneckFinding Bottleneck { get; } = bottleneck;

        public OcpRawEvidence Raw { get; } = raw;
    }
}
