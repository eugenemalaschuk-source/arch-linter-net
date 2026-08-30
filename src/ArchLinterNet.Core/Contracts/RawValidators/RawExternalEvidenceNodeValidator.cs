using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Contracts.Validators;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// `IgnoreUnmatchedProperties()` would otherwise silently discard a misspelled external-evidence
// declaration member. Keep this raw pass focused on the closed YAML shape and scalar values that
// would otherwise be lost or produce an unhelpful deserialization error; semantic interpretation
// (including the supported format) is checked after deserialization by
// ExternalEvidencePolicyValidator.
internal sealed class RawExternalEvidenceNodeValidator : IArchitecturePolicyRawDocumentValidator
{
    private const string ExternalEvidenceKey = "external_evidence";

    private static readonly string[] _allowedKeys =
    [
        "id", "format", "required", "tool", "tool_version", "run",
        "require_repository", "require_revision", "require_scope", "diagnostic_filter",
    ];

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        if (document.Root is null || !RawYamlNodes.TryGetChild(document.Root, ExternalEvidenceKey, out YamlNode? node))
        {
            return;
        }

        string sectionPath = ArchitecturePolicyProvenancePath.Property(ExternalEvidenceKey);
        document.Provenance.SetValidationSubject(sectionPath);
        if (node is not YamlSequenceNode entries)
        {
            throw new InvalidOperationException("external_evidence must be a list of objects.");
        }

        for (int index = 0; index < entries.Children.Count; index++)
        {
            string entryPath = ArchitecturePolicyProvenancePath.AppendIndex(sectionPath, index);
            document.Provenance.SetValidationSubject(entryPath);
            if (entries.Children[index] is not YamlMappingNode entry)
            {
                throw new InvalidOperationException($"external_evidence entry {index} must be an object.");
            }

            ValidateKeys(entry, index);
            SetFieldValidationSubject(document, entryPath, entry, "id");
            ValidateString(entry, "id", index, requireNonBlank: true);
            SetFieldValidationSubject(document, entryPath, entry, "format");
            ValidateString(entry, "format", index, requireNonBlank: true);
            SetFieldValidationSubject(document, entryPath, entry, "required");
            ValidateBoolean(entry, "required", index);
            SetFieldValidationSubject(document, entryPath, entry, "tool");
            ValidateString(entry, "tool", index, requireNonBlank: true);
            SetFieldValidationSubject(document, entryPath, entry, "tool_version");
            ValidateString(entry, "tool_version", index, requireNonBlank: false, optional: true);
            SetFieldValidationSubject(document, entryPath, entry, "run");
            ValidateString(entry, "run", index, requireNonBlank: true);
            SetFieldValidationSubject(document, entryPath, entry, "require_repository");
            ValidateBoolean(entry, "require_repository", index, optional: true);
            SetFieldValidationSubject(document, entryPath, entry, "require_revision");
            ValidateBoolean(entry, "require_revision", index, optional: true);
            SetFieldValidationSubject(document, entryPath, entry, "require_scope");
            ValidateBoolean(entry, "require_scope", index, optional: true);
            SetFieldValidationSubject(document, entryPath, entry, "diagnostic_filter");
            if (RawYamlNodes.TryGetChild(entry, "diagnostic_filter", out YamlNode? filterNode))
            {
                ValidateDiagnosticFilter(document, filterNode, index, entryPath);
            }
        }
    }

    private static void ValidateDiagnosticFilter(
        ArchitecturePolicyRawDocument document,
        YamlNode filterNode,
        int entryIndex,
        string entryPath)
    {
        string filterPath = ArchitecturePolicyProvenancePath.AppendProperty(entryPath, "diagnostic_filter");
        document.Provenance.SetValidationSubject(filterPath);
        if (filterNode is not YamlMappingNode filter)
        {
            throw new InvalidOperationException(
                $"external_evidence entry {entryIndex} diagnostic_filter must be an object.");
        }

        ValidateFilterKeys(filter, entryIndex);
        ValidateStringList(document, filter, filterPath, "rule_ids", entryIndex, optional: true);
        ValidateStringList(document, filter, filterPath, "rule_tags", entryIndex, optional: true);
        ValidateStringList(document, filter, filterPath, "projects", entryIndex, optional: true);
        ValidateStringList(document, filter, filterPath, "path_prefixes", entryIndex, optional: true, pathPrefixes: true);
        ValidateSeverity(document, filter, filterPath, entryIndex);
        SetFilterFieldValidationSubject(document, filterPath, filter, "require_matches");
        ValidateBoolean(filter, "require_matches", entryIndex, optional: true, fieldPrefix: "diagnostic_filter");
    }

    private static void ValidateFilterKeys(YamlMappingNode filter, int entryIndex)
    {
        string[] allowed = ["rule_ids", "rule_tags", "projects", "path_prefixes", "severity", "require_matches"];
        foreach ((YamlNode keyNode, _) in filter.Children)
        {
            if (keyNode is not YamlScalarNode key || !allowed.Contains(key.Value, StringComparer.Ordinal))
            {
                string rendered = keyNode is YamlScalarNode scalar ? scalar.Value ?? "<null>" : "<non-scalar>";
                throw new InvalidOperationException(
                    $"external_evidence entry {entryIndex} diagnostic_filter contains unknown property '{rendered}'.");
            }
        }
    }

    private static void ValidateStringList(
        ArchitecturePolicyRawDocument document,
        YamlMappingNode filter,
        string filterPath,
        string key,
        int entryIndex,
        bool optional,
        bool pathPrefixes = false)
    {
        if (!RawYamlNodes.TryGetChild(filter, key, out YamlNode? node))
        {
            if (!optional)
            {
                throw new InvalidOperationException(
                    $"external_evidence entry {entryIndex} diagnostic_filter must declare '{key}'.");
            }

            return;
        }

        string fieldPath = ArchitecturePolicyProvenancePath.AppendProperty(filterPath, key);
        document.Provenance.SetValidationSubject(fieldPath);
        if (node is not YamlSequenceNode values)
        {
            throw new InvalidOperationException(
                $"external_evidence[{entryIndex}].diagnostic_filter.{key} must be a list of strings.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int valueIndex = 0; valueIndex < values.Children.Count; valueIndex++)
        {
            string valuePath = ArchitecturePolicyProvenancePath.AppendIndex(fieldPath, valueIndex);
            document.Provenance.SetValidationSubject(valuePath);
            if (values.Children[valueIndex] is not YamlScalarNode scalar
                || RawYamlNodes.IsExplicitNull(scalar)
                || !IsStringScalar(scalar)
                || string.IsNullOrWhiteSpace(scalar.Value))
            {
                throw new InvalidOperationException(
                    $"external_evidence[{entryIndex}].diagnostic_filter.{key}[{valueIndex}] must be a non-blank string.");
            }

            string value = scalar.Value!;
            if (!seen.Add(value))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry {entryIndex} diagnostic_filter.{key} declares duplicate value '{value}'.");
            }

            if (pathPrefixes && !ExternalDiagnosticFilterRules.IsSafePathPrefix(value))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry {entryIndex} diagnostic_filter.path_prefixes value '{value}' must be a safe repository-relative slash-normalized path prefix.");
            }
        }
    }

    private static void ValidateSeverity(
        ArchitecturePolicyRawDocument document,
        YamlMappingNode filter,
        string filterPath,
        int entryIndex)
    {
        const string Key = "severity";
        string fieldPath = ArchitecturePolicyProvenancePath.AppendProperty(filterPath, Key);
        SetFilterFieldValidationSubject(document, filterPath, filter, Key);
        if (!RawYamlNodes.TryGetChild(filter, Key, out YamlNode? node))
        {
            throw new InvalidOperationException(
                $"external_evidence entry {entryIndex} diagnostic_filter must declare 'severity'.");
        }

        if (node is not YamlMappingNode severity || severity.Children.Count == 0)
        {
            throw new InvalidOperationException(
                $"external_evidence[{entryIndex}].diagnostic_filter.severity must be a non-empty map.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach ((YamlNode keyNode, YamlNode modeNode) in severity.Children)
        {
            document.Provenance.SetValidationSubject(fieldPath);
            if (keyNode is not YamlScalarNode severityKey
                || RawYamlNodes.IsExplicitNull(severityKey)
                || !IsStringScalar(severityKey)
                || string.IsNullOrWhiteSpace(severityKey.Value))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry {entryIndex} diagnostic_filter.severity keys must be non-blank strings.");
            }

            string sourceSeverity = severityKey.Value!;
            if (!seen.Add(sourceSeverity))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry {entryIndex} diagnostic_filter.severity declares duplicate key '{sourceSeverity}'.");
            }

            string severityEntryPath = ArchitecturePolicyProvenancePath.AppendProperty(fieldPath, sourceSeverity);
            document.Provenance.SetValidationSubject(severityEntryPath);
            if (!ExternalDiagnosticFilterRules.SupportedSeverities.Contains(sourceSeverity, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry {entryIndex} diagnostic_filter.severity key '{sourceSeverity}' is unsupported.");
            }

            if (modeNode is not YamlScalarNode mode
                || RawYamlNodes.IsExplicitNull(mode)
                || !IsStringScalar(mode)
                || string.IsNullOrWhiteSpace(mode.Value))
            {
                throw new InvalidOperationException(
                    $"external_evidence[{entryIndex}].diagnostic_filter.severity.{sourceSeverity} must be a non-blank mode string.");
            }

            if (!ExternalDiagnosticFilterRules.SupportedModes.Contains(mode.Value!, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"external_evidence entry {entryIndex} diagnostic_filter.severity.{sourceSeverity} mode '{mode.Value}' is unsupported.");
            }
        }
    }

    private static void SetFilterFieldValidationSubject(
        ArchitecturePolicyRawDocument document,
        string filterPath,
        YamlMappingNode filter,
        string key)
    {
        document.Provenance.SetValidationSubject(
            RawYamlNodes.TryGetChild(filter, key, out _)
                ? ArchitecturePolicyProvenancePath.AppendProperty(filterPath, key)
                : filterPath);
    }

    private static bool IsStringScalar(YamlScalarNode scalar)
    {
        if (scalar.Style != ScalarStyle.Plain || !scalar.Tag.IsNonSpecific || scalar.Value is null)
        {
            return true;
        }

        string value = scalar.Value;
        return !bool.TryParse(value, out _)
            && !long.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out _)
            && !double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    private static void SetFieldValidationSubject(
        ArchitecturePolicyRawDocument document,
        string entryPath,
        YamlMappingNode entry,
        string key)
    {
        document.Provenance.SetValidationSubject(
            RawYamlNodes.TryGetChild(entry, key, out _)
                ? ArchitecturePolicyProvenancePath.AppendProperty(entryPath, key)
                : entryPath);
    }

    private static void ValidateKeys(YamlMappingNode entry, int index)
    {
        foreach ((YamlNode keyNode, _) in entry.Children)
        {
            if (keyNode is not YamlScalarNode key
                || !_allowedKeys.Contains(key.Value, StringComparer.Ordinal))
            {
                string rendered = keyNode is YamlScalarNode scalar ? scalar.Value ?? "<null>" : "<non-scalar>";
                throw new InvalidOperationException(
                    $"external_evidence entry {index} contains unknown property '{rendered}'.");
            }
        }
    }

    private static void ValidateString(
        YamlMappingNode entry,
        string key,
        int index,
        bool requireNonBlank,
        bool optional = false)
    {
        if (!RawYamlNodes.TryGetChild(entry, key, out YamlNode? node))
        {
            if (!optional)
            {
                throw new InvalidOperationException($"external_evidence entry {index} must declare '{key}'.");
            }

            return;
        }

        string location = $"external_evidence[{index}].{key}";
        if (node is not YamlScalarNode scalar
            || RawYamlNodes.IsExplicitNull(scalar)
            || (requireNonBlank && string.IsNullOrWhiteSpace(scalar.Value)))
        {
            throw new InvalidOperationException(
                $"{location} must be {(requireNonBlank ? "a non-blank" : "a non-null")} string.");
        }

        if (requireNonBlank)
        {
            return;
        }

        // An explicitly supplied optional version still has to be a meaningful value. Empty
        // strings are indistinguishable from an omitted selector at the reader boundary.
        if (string.IsNullOrWhiteSpace(scalar.Value))
        {
            throw new InvalidOperationException($"{location} must be a non-blank string when declared.");
        }
    }

    private static void ValidateBoolean(
        YamlMappingNode entry,
        string key,
        int index,
        bool optional = false,
        string fieldPrefix = "")
    {
        if (!RawYamlNodes.TryGetChild(entry, key, out YamlNode? node))
        {
            if (!optional)
            {
                throw new InvalidOperationException($"external_evidence entry {index} must declare '{key}'.");
            }

            return;
        }

        string location = string.IsNullOrEmpty(fieldPrefix)
            ? $"external_evidence[{index}].{key}"
            : $"external_evidence[{index}].{fieldPrefix}.{key}";
        if (node is not YamlScalarNode scalar
            || scalar.Style != ScalarStyle.Plain
            || RawYamlNodes.IsExplicitNull(scalar)
            || (!string.Equals(scalar.Value, "true", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(scalar.Value, "false", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"{location} must be a boolean.");
        }
    }
}
