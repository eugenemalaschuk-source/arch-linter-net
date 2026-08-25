using System.Globalization;
using ArchLinterNet.Core.Contracts.PolicyImports;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// The normal schema is evaluated for composed policies, but monolithic policies intentionally skip
// that pass. This mirrors the other raw validators so an unknown or non-canonical history-analysis
// key cannot be silently discarded by YamlDotNet's IgnoreUnmatchedProperties setting.
internal sealed class RawHistoryAnalysisNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private const string HistoryAnalysisKey = "history_analysis";
    private const string ExtractorsKey = "extractors";
    private const string PathsKey = "paths";
    private const string ThresholdsKey = "thresholds";
    private const string PatternKey = "pattern";

    private static readonly string[] _historyAnalysisKeys = [ExtractorsKey, PathsKey, "ignore", "weights", ThresholdsKey];
    private static readonly string[] _extractorKeys = ["id", "namespace", PatternKey];
    private static readonly string[] _patternKeys = ["prefix", "suffix"];
    private static readonly string[] _pathKeys = ["production", "tests", "docs", "generated", "build_ci", "samples_examples"];
    private static readonly string[] _weightKeys = ["hotspot", "co_change", "bottleneck", "ocp"];
    private static readonly string[] _thresholdKeys = ["co_change_significance"];

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        if (document.Root is null || !RawYamlNodes.TryGetChild(document.Root, HistoryAnalysisKey, out YamlNode? historyNode))
        {
            return;
        }

        document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey));
        YamlMappingNode history = RequireMapping(historyNode, HistoryAnalysisKey);
        ValidateKeys(history, _historyAnalysisKeys, HistoryAnalysisKey);
        ValidateExtractors(history, document);
        ValidatePaths(history, document);
        ValidateStringList(
            history,
            "ignore",
            "history_analysis.ignore",
            ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey), "ignore"),
            document);
        ValidateWeights(history, document);
        ValidateThresholds(history, document);
    }

    private static void ValidateExtractors(YamlMappingNode history, ArchitecturePolicyRawDocument document)
    {
        if (!RawYamlNodes.TryGetChild(history, ExtractorsKey, out YamlNode? extractorsNode))
        {
            return;
        }

        document.Provenance.SetValidationSubject(Property(
            ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey), ExtractorsKey));
        if (extractorsNode is not YamlSequenceNode extractors)
        {
            throw new InvalidOperationException("history_analysis.extractors must be a list.");
        }

        for (int index = 0; index < extractors.Children.Count; index++)
        {
            string extractorPath = Path(ExtractorsKey, index);
            document.Provenance.SetValidationSubject(extractorPath);
            YamlMappingNode extractor = RequireMapping(extractors.Children[index], $"history_analysis.extractors[{index}]");
            ValidateKeys(extractor, _extractorKeys, $"history_analysis.extractors[{index}]");
            document.Provenance.SetValidationSubject(Property(extractorPath, "id"));
            RequireString(extractor, "id", $"history_analysis.extractors[{index}].id");
            document.Provenance.SetValidationSubject(Property(extractorPath, "namespace"));
            RequireString(extractor, "namespace", $"history_analysis.extractors[{index}].namespace");

            document.Provenance.SetValidationSubject(Property(extractorPath, PatternKey));
            if (!RawYamlNodes.TryGetChild(extractor, PatternKey, out YamlNode? patternNode))
            {
                throw new InvalidOperationException($"history_analysis.extractors[{index}] must declare pattern.");
            }

            YamlMappingNode pattern = RequireMapping(patternNode, $"history_analysis.extractors[{index}].pattern");
            ValidateKeys(pattern, _patternKeys, $"history_analysis.extractors[{index}].pattern");
            document.Provenance.SetValidationSubject(Property(Property(extractorPath, PatternKey), "prefix"));
            RequireString(pattern, "prefix", $"history_analysis.extractors[{index}].pattern.prefix", requireNonEmpty: true);
            document.Provenance.SetValidationSubject(Property(Property(extractorPath, PatternKey), "suffix"));
            OptionalString(pattern, "suffix", $"history_analysis.extractors[{index}].pattern.suffix");
        }
    }

    private static void ValidatePaths(YamlMappingNode history, ArchitecturePolicyRawDocument document)
    {
        if (!RawYamlNodes.TryGetChild(history, PathsKey, out YamlNode? pathsNode))
        {
            return;
        }

        document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.AppendProperty(
            ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey), PathsKey));
        YamlMappingNode paths = RequireMapping(pathsNode, "history_analysis.paths");
        ValidateKeys(paths, _pathKeys, "history_analysis.paths");
        foreach (string pathKey in _pathKeys)
        {
            string effectivePath = Property(
                Property(ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey), PathsKey),
                pathKey);
            document.Provenance.SetValidationSubject(effectivePath);
            ValidateStringList(paths, pathKey, $"history_analysis.paths.{pathKey}", effectivePath, document);
        }
    }

    private static void ValidateWeights(YamlMappingNode history, ArchitecturePolicyRawDocument document)
    {
        if (!RawYamlNodes.TryGetChild(history, "weights", out YamlNode? weightsNode))
        {
            return;
        }

        document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.AppendProperty(
            ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey), "weights"));
        YamlMappingNode weights = RequireMapping(weightsNode, "history_analysis.weights");
        ValidateKeys(weights, _weightKeys, "history_analysis.weights");
        ValidateProfile(weights, "hotspot", ["commit", "churn", "task", "author", "temporal"], document);
        ValidateProfile(weights, "co_change", ["commit", "task"], document);
        ValidateProfile(weights, "bottleneck", ["independent_task", "author", "temporal", "degree", "centrality"], document);
        ValidateProfile(weights, "ocp", ["independent_task", "centrality", "repeated_edit", "role_hint"], document);
    }

    private static void ValidateProfile(
        YamlMappingNode weights,
        string profileName,
        IReadOnlyList<string> keys,
        ArchitecturePolicyRawDocument document)
    {
        if (!RawYamlNodes.TryGetChild(weights, profileName, out YamlNode? profileNode))
        {
            return;
        }

        document.Provenance.SetValidationSubject(Property(
            Property(ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey), "weights"),
            profileName));
        YamlMappingNode profile = RequireMapping(profileNode, $"history_analysis.weights.{profileName}");
        ValidateKeys(profile, keys, $"history_analysis.weights.{profileName}");
        foreach (string key in keys)
        {
            document.Provenance.SetValidationSubject(Property(
                Property(
                    Property(ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey), "weights"),
                    profileName),
                key));
            if (!RawYamlNodes.TryGetChild(profile, key, out YamlNode? valueNode))
            {
                throw new InvalidOperationException($"history_analysis.weights.{profileName} must declare '{key}'.");
            }

            RequireDecimal(valueNode, $"history_analysis.weights.{profileName}.{key}");
        }
    }

    private static void ValidateThresholds(YamlMappingNode history, ArchitecturePolicyRawDocument document)
    {
        if (!RawYamlNodes.TryGetChild(history, ThresholdsKey, out YamlNode? thresholdsNode))
        {
            return;
        }

        document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.AppendProperty(
            ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey), ThresholdsKey));
        YamlMappingNode thresholds = RequireMapping(thresholdsNode, "history_analysis.thresholds");
        ValidateKeys(thresholds, _thresholdKeys, "history_analysis.thresholds");
        if (RawYamlNodes.TryGetChild(thresholds, "co_change_significance", out YamlNode? threshold))
        {
            document.Provenance.SetValidationSubject(Property(
                Property(ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey), ThresholdsKey),
                "co_change_significance"));
            RequireDecimal(threshold, "history_analysis.thresholds.co_change_significance");
        }
    }

    private static void ValidateStringList(
        YamlMappingNode parent,
        string key,
        string location,
        string effectivePath,
        ArchitecturePolicyRawDocument document)
    {
        if (!RawYamlNodes.TryGetChild(parent, key, out YamlNode? node))
        {
            return;
        }

        if (node is not YamlSequenceNode values)
        {
            throw new InvalidOperationException($"{location} must be a list of non-empty strings.");
        }

        for (int index = 0; index < values.Children.Count; index++)
        {
            document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.AppendIndex(effectivePath, index));
            if (values.Children[index] is not YamlScalarNode scalar
                || RawYamlNodes.IsExplicitNull(scalar)
                || string.IsNullOrWhiteSpace(scalar.Value))
            {
                throw new InvalidOperationException($"{location} entries must be non-empty strings.");
            }
        }
    }

    private static YamlMappingNode RequireMapping(YamlNode node, string location) =>
        node as YamlMappingNode ?? throw new InvalidOperationException($"{location} must be an object.");

    private static void RequireString(YamlMappingNode parent, string key, string location, bool requireNonEmpty = false)
    {
        if (!RawYamlNodes.TryGetChild(parent, key, out YamlNode? node))
        {
            throw new InvalidOperationException($"{location} is required.");
        }

        OptionalStringNode(node, location, requireNonEmpty);
    }

    private static void OptionalString(YamlMappingNode parent, string key, string location)
    {
        if (RawYamlNodes.TryGetChild(parent, key, out YamlNode? node))
        {
            OptionalStringNode(node, location, requireNonEmpty: false);
        }
    }

    private static void OptionalStringNode(YamlNode node, string location, bool requireNonEmpty)
    {
        if (node is not YamlScalarNode scalar
            || RawYamlNodes.IsExplicitNull(scalar)
            || (requireNonEmpty && string.IsNullOrEmpty(scalar.Value)))
        {
            throw new InvalidOperationException($"{location} must be {(requireNonEmpty ? "a non-empty" : "a")} string.");
        }
    }

    private static void RequireDecimal(YamlNode node, string location)
    {
        if (node is not YamlScalarNode scalar
            || scalar.Style != ScalarStyle.Plain
            || !IsPlainDecimal(scalar.Value)
            || !decimal.TryParse(scalar.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
        {
            throw new InvalidOperationException(
                $"{location} must be a nonnegative base-10 decimal with at most nine fractional digits.");
        }
    }

    private static bool IsPlainDecimal(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        int decimalPoint = value.IndexOf('.');
        if (decimalPoint >= 0 && (value.Length - decimalPoint - 1 is < 1 or > 9))
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] == '.' && index == decimalPoint)
            {
                continue;
            }

            if (value[index] is < '0' or > '9')
            {
                return false;
            }
        }

        return decimalPoint == value.LastIndexOf('.');
    }

    private static void ValidateKeys(YamlMappingNode mapping, IEnumerable<string> allowed, string location)
    {
        foreach ((YamlNode key, _) in mapping.Children)
        {
            if (key is not YamlScalarNode scalar || !allowed.Contains(scalar.Value, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"{location} contains an unknown property '{(key as YamlScalarNode)?.Value}'.");
            }
        }
    }

    private static string Path(string property, int index) => ArchitecturePolicyProvenancePath.AppendIndex(
        ArchitecturePolicyProvenancePath.AppendProperty(ArchitecturePolicyProvenancePath.Property(HistoryAnalysisKey), property), index);

    private static string Property(string parent, string property) =>
        ArchitecturePolicyProvenancePath.AppendProperty(parent, property);
}
