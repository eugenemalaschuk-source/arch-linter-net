using ArchLinterNet.Core.Contracts.PolicyImports;
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
        "require_repository", "require_revision", "require_scope",
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
        }
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

    private static void ValidateBoolean(YamlMappingNode entry, string key, int index, bool optional = false)
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
            || scalar.Style != ScalarStyle.Plain
            || RawYamlNodes.IsExplicitNull(scalar)
            || (!string.Equals(scalar.Value, "true", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(scalar.Value, "false", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"{location} must be a boolean.");
        }
    }
}
