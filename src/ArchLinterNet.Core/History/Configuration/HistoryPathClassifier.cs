using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.History.Configuration;

internal enum HistoryPathCategory
{
    Production,
    Tests,
    Docs,
    Generated,
    BuildCi,
    SamplesExamples,
    Unknown,
}

internal readonly record struct HistoryPathClassification(bool IsIgnored, HistoryPathCategory Category);

internal sealed class HistoryPathClassifier
{
    private readonly IReadOnlyList<HistoryPathGlob> _ignored;
    private readonly IReadOnlyList<(HistoryPathCategory Category, IReadOnlyList<HistoryPathGlob> Patterns)> _categories;

    public HistoryPathClassifier(HistoryAnalysisConfiguration configuration)
    {
        _ignored = configuration.Ignore.Select(HistoryPathGlob.Parse).ToArray();
        _categories =
        [
            (HistoryPathCategory.Production, Compile(configuration.Paths.Production)),
            (HistoryPathCategory.Tests, Compile(configuration.Paths.Tests)),
            (HistoryPathCategory.Docs, Compile(configuration.Paths.Docs)),
            (HistoryPathCategory.Generated, Compile(configuration.Paths.Generated)),
            (HistoryPathCategory.BuildCi, Compile(configuration.Paths.BuildCi)),
            (HistoryPathCategory.SamplesExamples, Compile(configuration.Paths.SamplesExamples)),
        ];
    }

    public HistoryPathClassification Classify(string canonicalPath)
    {
        if (_ignored.Any(pattern => pattern.IsMatch(canonicalPath)))
        {
            return new HistoryPathClassification(IsIgnored: true, HistoryPathCategory.Unknown);
        }

        foreach ((HistoryPathCategory category, IReadOnlyList<HistoryPathGlob> patterns) in _categories)
        {
            if (patterns.Any(pattern => pattern.IsMatch(canonicalPath)))
            {
                return new HistoryPathClassification(IsIgnored: false, category);
            }
        }

        return new HistoryPathClassification(IsIgnored: false, HistoryPathCategory.Unknown);
    }

    private static HistoryPathGlob[] Compile(IEnumerable<string> patterns) =>
        patterns.Select(HistoryPathGlob.Parse).ToArray();
}
