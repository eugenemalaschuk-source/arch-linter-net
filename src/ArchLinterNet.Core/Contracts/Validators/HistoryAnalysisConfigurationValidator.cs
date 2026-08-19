using System.Text.RegularExpressions;
using ArchLinterNet.Core.History.Configuration;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed partial class HistoryAnalysisConfigurationValidator : IArchitecturePolicyDocumentValidator
{
    private const decimal RequiredProfileTotal = 1.000000000m;

    public void Validate(ArchitectureContractDocument document)
    {
        HistoryAnalysisConfiguration configuration = document.HistoryAnalysis;
        ValidateExtractors(configuration.Extractors);
        ValidatePaths(configuration);
        ValidateProfiles(configuration.Weights);
        ValidateThreshold(configuration.Thresholds.CoChangeSignificance);
    }

    private static void ValidateExtractors(IReadOnlyList<HistoryTaskExtractorConfiguration> extractors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (HistoryTaskExtractorConfiguration extractor in extractors)
        {
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

            if (!StableIdentifierPattern().IsMatch(extractor.Namespace))
            {
                throw new InvalidOperationException("history_analysis extractor namespaces must match '[a-z][a-z0-9._-]*'.");
            }

            if (string.IsNullOrEmpty(extractor.Pattern.Prefix))
            {
                throw new InvalidOperationException("history_analysis extractor pattern.prefix must be non-empty.");
            }

            if (extractor.Pattern.Suffix.Length > 0 && IsAsciiDigit(extractor.Pattern.Suffix[0]))
            {
                throw new InvalidOperationException("history_analysis extractor pattern.suffix must not start with an ASCII digit.");
            }
        }
    }

    private static void ValidatePaths(HistoryAnalysisConfiguration configuration)
    {
        foreach (string pattern in configuration.Ignore)
        {
            HistoryPathGlob.Parse(pattern);
        }

        foreach (IReadOnlyList<string> patterns in new IReadOnlyList<string>[]
        {
            configuration.Paths.Production,
            configuration.Paths.Tests,
            configuration.Paths.Docs,
            configuration.Paths.Generated,
            configuration.Paths.BuildCi,
            configuration.Paths.SamplesExamples,
        })
        {
            foreach (string pattern in patterns)
            {
                HistoryPathGlob.Parse(pattern);
            }
        }
    }

    private static void ValidateProfiles(HistoryAnalysisWeightProfiles profiles)
    {
        ValidateProfile("hotspot", [profiles.Hotspot.Commit, profiles.Hotspot.Churn, profiles.Hotspot.Task, profiles.Hotspot.Author, profiles.Hotspot.Temporal]);
        ValidateProfile("co_change", [profiles.CoChange.Commit, profiles.CoChange.Task]);
        ValidateProfile("bottleneck", [profiles.Bottleneck.IndependentTask, profiles.Bottleneck.Author, profiles.Bottleneck.Temporal, profiles.Bottleneck.Degree, profiles.Bottleneck.Centrality]);
        ValidateProfile("ocp", [profiles.Ocp.IndependentTask, profiles.Ocp.Centrality, profiles.Ocp.RepeatedEdit, profiles.Ocp.RoleHint]);
    }

    private static void ValidateProfile(string profileName, IReadOnlyList<decimal> values)
    {
        if (values.Any(static value => value < 0m))
        {
            throw new InvalidOperationException($"history_analysis {profileName} weights must be nonnegative.");
        }

        if (values.All(static value => value == 0m))
        {
            throw new InvalidOperationException($"history_analysis {profileName} weights must enable at least one component.");
        }

        if (values.Aggregate(0m, static (total, value) => total + value) != RequiredProfileTotal)
        {
            throw new InvalidOperationException($"history_analysis {profileName} weights must sum exactly to 1.000000000.");
        }
    }

    private static void ValidateThreshold(decimal? threshold)
    {
        if (threshold is < 0m or > 1m)
        {
            throw new InvalidOperationException("history_analysis thresholds.co_change_significance must be in [0,1].");
        }
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    [GeneratedRegex("^[a-z][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex StableIdentifierPattern();
}
