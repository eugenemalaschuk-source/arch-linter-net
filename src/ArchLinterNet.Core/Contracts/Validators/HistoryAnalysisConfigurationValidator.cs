using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.History.Configuration;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed partial class HistoryAnalysisConfigurationValidator : IArchitecturePolicyDocumentValidator
{
    private const decimal RequiredProfileTotal = 1.000000000m;

    public void Validate(ArchitectureContractDocument document)
    {
        HistoryAnalysisConfiguration configuration = document.HistoryAnalysis;
        ValidateExtractors(document, configuration.Extractors);
        ValidatePaths(document, configuration);
        ValidateProfiles(document, configuration.Weights);
        ValidateThreshold(document, configuration.Thresholds.CoChangeSignificance);
    }

    private static void ValidateExtractors(
        ArchitectureContractDocument document,
        List<HistoryTaskExtractorConfiguration> extractors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < extractors.Count; index++)
        {
            HistoryTaskExtractorConfiguration extractor = extractors[index];
            string extractorPath = Path("extractors", index);

            document.Provenance.SetValidationSubject(Property(extractorPath, "id"));
            if (!StableIdentifierPattern().IsMatch(extractor.Id))
            {
                throw new InvalidOperationException("history_analysis extractor IDs must match '[a-z][a-z0-9._-]*'.");
            }

            if (string.Equals(extractor.Id, "issue", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("history_analysis extractor ID 'issue' is reserved for the built-in extractor.");
            }

            if (!ids.Add(extractor.Id))
            {
                throw new InvalidOperationException($"history_analysis extractor ID '{extractor.Id}' is duplicated.");
            }

            document.Provenance.SetValidationSubject(Property(extractorPath, "namespace"));
            if (!StableIdentifierPattern().IsMatch(extractor.Namespace))
            {
                throw new InvalidOperationException("history_analysis extractor namespaces must match '[a-z][a-z0-9._-]*'.");
            }

            document.Provenance.SetValidationSubject(Property(Property(extractorPath, "pattern"), "prefix"));
            if (string.IsNullOrEmpty(extractor.Pattern.Prefix))
            {
                throw new InvalidOperationException("history_analysis extractor pattern.prefix must be non-empty.");
            }

            document.Provenance.SetValidationSubject(Property(Property(extractorPath, "pattern"), "suffix"));
            if (extractor.Pattern.Suffix.Length > 0 && IsAsciiDigit(extractor.Pattern.Suffix[0]))
            {
                throw new InvalidOperationException("history_analysis extractor pattern.suffix must not start with an ASCII digit.");
            }
        }
    }

    private static void ValidatePaths(ArchitectureContractDocument document, HistoryAnalysisConfiguration configuration)
    {
        ValidatePathPatterns(document, "ignore", configuration.Ignore);

        foreach ((string category, IReadOnlyList<string> patterns) in new (string, IReadOnlyList<string>)[]
        {
            ("production", configuration.Paths.Production),
            ("tests", configuration.Paths.Tests),
            ("docs", configuration.Paths.Docs),
            ("generated", configuration.Paths.Generated),
            ("build_ci", configuration.Paths.BuildCi),
            ("samples_examples", configuration.Paths.SamplesExamples),
        })
        {
            ValidatePathPatterns(document, $"paths.{category}", patterns);
        }
    }

    private static void ValidatePathPatterns(
        ArchitectureContractDocument document,
        string relativePath,
        IReadOnlyList<string> patterns)
    {
        for (int index = 0; index < patterns.Count; index++)
        {
            document.Provenance.SetValidationSubject(Path(relativePath, index));
            HistoryPathGlob.Parse(patterns[index]);
        }
    }

    private static void ValidateProfiles(ArchitectureContractDocument document, HistoryAnalysisWeightProfiles profiles)
    {
        ValidateProfile(document, "hotspot", [
            ("commit", profiles.Hotspot.Commit), ("churn", profiles.Hotspot.Churn),
            ("task", profiles.Hotspot.Task), ("author", profiles.Hotspot.Author), ("temporal", profiles.Hotspot.Temporal),
        ]);
        ValidateProfile(document, "co_change", [("commit", profiles.CoChange.Commit), ("task", profiles.CoChange.Task)]);
        ValidateProfile(document, "bottleneck", [
            ("independent_task", profiles.Bottleneck.IndependentTask), ("author", profiles.Bottleneck.Author),
            ("temporal", profiles.Bottleneck.Temporal), ("degree", profiles.Bottleneck.Degree),
            ("centrality", profiles.Bottleneck.Centrality),
        ]);
        ValidateProfile(document, "ocp", [
            ("independent_task", profiles.Ocp.IndependentTask), ("centrality", profiles.Ocp.Centrality),
            ("repeated_edit", profiles.Ocp.RepeatedEdit), ("role_hint", profiles.Ocp.RoleHint),
        ]);
    }

    private static void ValidateProfile(
        ArchitectureContractDocument document,
        string profileName,
        IReadOnlyList<(string Name, decimal Value)> values)
    {
        foreach ((string name, decimal value) in values)
        {
            document.Provenance.SetValidationSubject(Path($"weights.{profileName}.{name}"));
            if (value < 0m)
            {
                throw new InvalidOperationException($"history_analysis {profileName} weights must be nonnegative.");
            }
        }

        document.Provenance.SetValidationSubject(Path($"weights.{profileName}"));
        if (values.All(static value => value.Value == 0m))
        {
            throw new InvalidOperationException($"history_analysis {profileName} weights must enable at least one component.");
        }

        if (values.Aggregate(0m, static (total, value) => total + value.Value) != RequiredProfileTotal)
        {
            throw new InvalidOperationException($"history_analysis {profileName} weights must sum exactly to 1.000000000.");
        }
    }

    private static void ValidateThreshold(ArchitectureContractDocument document, decimal? threshold)
    {
        document.Provenance.SetValidationSubject(Path("thresholds.co_change_significance"));
        if (threshold is < 0m or > 1m)
        {
            throw new InvalidOperationException("history_analysis thresholds.co_change_significance must be in [0,1].");
        }
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static string Path(string relativePath, int? index = null)
    {
        string path = ArchitecturePolicyProvenancePath.Property("history_analysis");
        foreach (string property in relativePath.Split('.'))
        {
            path = ArchitecturePolicyProvenancePath.AppendProperty(path, property);
        }

        return index is null ? path : ArchitecturePolicyProvenancePath.AppendIndex(path, index.Value);
    }

    private static string Property(string parent, string property) =>
        ArchitecturePolicyProvenancePath.AppendProperty(parent, property);

    [GeneratedRegex("^[a-z][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex StableIdentifierPattern();
}
