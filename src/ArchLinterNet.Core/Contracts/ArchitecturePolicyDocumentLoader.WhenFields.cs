using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts;

// Raw-YAML validation for the `when` CEL expression field (openspec/specs/cel-policy-model/spec.md),
// split out of ArchitecturePolicyDocumentLoader.cs to keep both files under the repository's file-size
// lint budget (make/lint.mk CS_SIZE_LINT_ERROR_LINES). See
// openspec/changes/archive/2026-07-18-core-cel-integration and Contracts/Validators/ExpressionCompilationValidator.cs
// (the post-deserialization compile step this raw pass gates).
public sealed partial class ArchitecturePolicyDocumentLoader
{
    // Closed first-wave `when` locations from openspec/specs/cel-policy-model/spec.md. IgnoreUnmatchedProperties()
    // silently drops any YAML key that has no matching C# property, so a `when` field declared anywhere else
    // (e.g. `analysis.when`, a bare contract-level `contracts.strict[0].when`) would otherwise vanish during
    // deserialization instead of failing the load - this raw pass (run for every policy, not only composed ones,
    // unlike ArchitecturePolicyEffectiveSchemaValidator) is the only place that can still see it. Each entry is
    // the exact sequence of mapping-key segments from the document root to the selector node that is allowed to
    // declare `when`; "*" matches any single mapping key or sequence index at that position.
    private static readonly string[][] _allowedWhenLocations =
    {
        new[] { "layers", "*", "selector" },
        new[] { "contracts", "strict_context_dependencies", "*", "source" },
        new[] { "contracts", "strict_context_dependencies", "*", "forbidden", "*" },
        new[] { "contracts", "strict_context_dependencies", "*", "exclude", "*" },
        new[] { "contracts", "audit_context_dependencies", "*", "source" },
        new[] { "contracts", "audit_context_dependencies", "*", "forbidden", "*" },
        new[] { "contracts", "audit_context_dependencies", "*", "exclude", "*" },
        new[] { "contracts", "strict_context_allow_only", "*", "source" },
        new[] { "contracts", "strict_context_allow_only", "*", "allowed", "*" },
        new[] { "contracts", "strict_context_allow_only", "*", "exclude", "*" },
        new[] { "contracts", "audit_context_allow_only", "*", "source" },
        new[] { "contracts", "audit_context_allow_only", "*", "allowed", "*" },
        new[] { "contracts", "audit_context_allow_only", "*", "exclude", "*" },
    };

    // Top-level dictionaries keyed by an author-chosen, arbitrary name (layer name, external-dependency-group
    // name, package-group name) rather than a fixed schema property. A layer literally named "when" (e.g.
    // `layers: { when: { namespace: ... } }`) is a legitimate, previously-valid name - the walk must not treat
    // that name itself as the CEL expression marker, even though it resumes normal (name-is-schema-property)
    // checking one level deeper, inside that named entry's own mapping. The exemption is granted only at
    // structuralPath.Count == 0 (the true document root) - matching by name alone anywhere in the tree would
    // let an author suppress when/opaque-checking for an entire fabricated subtree by nesting a bogus "layers"
    // (or "packages"/"external_dependencies") key at an unrelated, unapproved location.
    private static readonly string[] _arbitraryNameGroupKeys = { "layers", "external_dependencies", "packages" };

    private static void ValidateRawWhenFieldLocations(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return;
        }

        WalkForWhenFields(root, new List<string>(), new List<string>(), childKeysAreArbitraryNames: false);
    }

    private static void WalkForWhenFields(
        YamlMappingNode node, List<string> structuralPath, List<string> displayPath, bool childKeysAreArbitraryNames)
    {
        foreach ((YamlNode keyNode, YamlNode valueNode) in node.Children)
        {
            if (keyNode is not YamlScalarNode { Value: { } key })
            {
                continue;
            }

            // Arbitrary names (layer/group names) are ordinary data, never the reserved "when" marker or an
            // opaque-value boundary - both checks below are schema-property concerns and only apply when this
            // node's own keys are fixed schema properties, not author-chosen names.
            if (!childKeysAreArbitraryNames)
            {
                if (string.Equals(key, WhenKey, StringComparison.Ordinal))
                {
                    ValidateWhenFieldDeclaration(structuralPath, displayPath, valueNode);
                    continue;
                }

                if (IsRecognizedOpaqueValueKey(key, structuralPath))
                {
                    continue;
                }
            }

            bool nextChildKeysAreArbitraryNames = !childKeysAreArbitraryNames
                && structuralPath.Count == 0
                && _arbitraryNameGroupKeys.Contains(key, StringComparer.Ordinal);

            if (valueNode is YamlMappingNode childMapping)
            {
                structuralPath.Add(key);
                displayPath.Add(key);
                WalkForWhenFields(childMapping, structuralPath, displayPath, nextChildKeysAreArbitraryNames);
                structuralPath.RemoveAt(structuralPath.Count - 1);
                displayPath.RemoveAt(displayPath.Count - 1);
            }
            else if (valueNode is YamlSequenceNode sequence)
            {
                structuralPath.Add(key);
                displayPath.Add(key);
                WalkSequenceForWhenFields(sequence, structuralPath, displayPath);
                structuralPath.RemoveAt(structuralPath.Count - 1);
                displayPath.RemoveAt(displayPath.Count - 1);
            }
        }
    }

    // Recurses through arbitrarily nested sequences (a sequence whose items are themselves sequences), not
    // just one level of "sequence of mappings" - a `when` hidden inside a doubly (or deeper) nested list, e.g.
    // an unrecognized `forbidden: [[{ role: X, when: "..." }]]`-shaped field, must still be found. Every
    // nesting level pushes its own "*" segment onto structuralPath, so a genuinely nested-sequence `when`
    // structurally can never match any single-"*"-per-level entry in _allowedWhenLocations - it is always
    // correctly rejected once found, exactly as intended for a location outside the closed list.
    private static void WalkSequenceForWhenFields(
        YamlSequenceNode sequence, List<string> structuralPath, List<string> displayPath)
    {
        structuralPath.Add("*");
        for (int index = 0; index < sequence.Children.Count; index++)
        {
            YamlNode item = sequence.Children[index];
            displayPath.Add(index.ToString(CultureInfo.InvariantCulture));
            if (item is YamlMappingNode itemMapping)
            {
                WalkForWhenFields(itemMapping, structuralPath, displayPath, childKeysAreArbitraryNames: false);
            }
            else if (item is YamlSequenceNode nestedSequence)
            {
                WalkSequenceForWhenFields(nestedSequence, structuralPath, displayPath);
            }

            displayPath.RemoveAt(displayPath.Count - 1);
        }

        structuralPath.RemoveAt(structuralPath.Count - 1);
    }

    // A key is a recognized, legitimate opaque (arbitrary user-content) value boundary only when the CURRENT
    // node's exact structural path is one of the fixed positions this schema actually declares that property
    // at - never by key name alone, and never by a sibling-key heuristic. A sibling-key check (e.g. "opaque if
    // a 'role' key sits next to it, or if it is the node's only key") is still bypassable anywhere in the tree
    // by wrapping the payload in a fabricated container that happens to satisfy the heuristic (e.g.
    // `extensions: { metadata: { when: "..." } }`, or `classification.extensions: { role: x, metadata: { when:
    // "..." } }`) - IgnoreUnmatchedProperties() silently drops the fabricated container together with the
    // `when` it shields, exactly like the direct-sibling case this whole raw pass exists to catch. Exact-path
    // matching closes that off: `extensions`/`classification.extensions` are not on either list below, so the
    // walk keeps descending into them and still finds the `when` underneath.
    //
    // The metadata-bearing selector/classification/coverage-exclusion locations mirror _allowedWhenLocations'
    // context-selector paths (role+metadata+when are declared together on the same node) plus every
    // metadata-bearing shape `when` is NOT approved on (port-boundary/adapter-binding selectors, classification
    // extraction entries, semantic-role coverage exclusions).
    private static readonly string[][] _recognizedOpaqueMetadataLocations = _allowedWhenLocations
        .Select(location => location.Append(MetadataKey).ToArray())
        .Concat(new[]
        {
            new[] { "contracts", "strict_port_boundaries", "*", "source", MetadataKey },
            new[] { "contracts", "strict_port_boundaries", "*", "allowed_seams", "*", MetadataKey },
            new[] { "contracts", "strict_port_boundaries", "*", "forbidden", "*", MetadataKey },
            new[] { "contracts", "strict_port_boundaries", "*", "exclude", "*", MetadataKey },
            new[] { "contracts", "strict_port_boundaries", "*", "target_context", MetadataKey },
            new[] { "contracts", "strict_port_boundaries", "*", "adapter_bindings", "*", "adapter", MetadataKey },
            new[] { "contracts", "strict_port_boundaries", "*", "adapter_bindings", "*", "expected_port", MetadataKey },
            new[] { "contracts", "strict_port_boundaries", "*", "adapter_bindings", "*", "allowed_contexts", "*", MetadataKey },
            new[] { "contracts", "audit_port_boundaries", "*", "source", MetadataKey },
            new[] { "contracts", "audit_port_boundaries", "*", "allowed_seams", "*", MetadataKey },
            new[] { "contracts", "audit_port_boundaries", "*", "forbidden", "*", MetadataKey },
            new[] { "contracts", "audit_port_boundaries", "*", "exclude", "*", MetadataKey },
            new[] { "contracts", "audit_port_boundaries", "*", "target_context", MetadataKey },
            new[] { "contracts", "audit_port_boundaries", "*", "adapter_bindings", "*", "adapter", MetadataKey },
            new[] { "contracts", "audit_port_boundaries", "*", "adapter_bindings", "*", "expected_port", MetadataKey },
            new[] { "contracts", "audit_port_boundaries", "*", "adapter_bindings", "*", "allowed_contexts", "*", MetadataKey },
            new[] { "classification", "attributes", "*", MetadataKey },
            new[] { "classification", "assembly_attributes", "*", MetadataKey },
            new[] { "classification", "inheritance", "*", MetadataKey },
            new[] { "classification", "namespace", "*", MetadataKey },
            new[] { "classification", "path", "*", MetadataKey },
            new[] { "classification", "overrides", "*", MetadataKey },
            new[] { "contracts", "strict_coverage", "*", "exclude", "*", MetadataKey },
            new[] { "contracts", "audit_coverage", "*", "exclude", "*", MetadataKey },
        })
        .ToArray();

    private static readonly string[][] _recognizedOpaqueScalarMapLocations =
    {
        new[] { "contracts", "strict_project_metadata", "*", "required_properties" },
        new[] { "contracts", "strict_project_metadata", "*", "forbidden_properties" },
        new[] { "contracts", "audit_project_metadata", "*", "required_properties" },
        new[] { "contracts", "audit_project_metadata", "*", "forbidden_properties" },
    };

    private static readonly string[][] _recognizedOpaqueConditionSetsLocations =
    {
        new[] { "analysis", "condition_sets" },
    };

    private static bool IsRecognizedOpaqueValueKey(string key, IReadOnlyList<string> structuralPath)
    {
        string[] ownPath = new List<string>(structuralPath) { key }.ToArray();
        return key switch
        {
            MetadataKey => MatchesAnyPattern(ownPath, _recognizedOpaqueMetadataLocations),
            "required_properties" or "forbidden_properties" => MatchesAnyPattern(ownPath, _recognizedOpaqueScalarMapLocations),
            "condition_sets" => MatchesAnyPattern(ownPath, _recognizedOpaqueConditionSetsLocations),
            _ => false,
        };
    }

    private static void ValidateWhenFieldDeclaration(List<string> structuralPath, List<string> displayPath, YamlNode valueNode)
    {
        string location = string.Join(".", displayPath.Append(WhenKey));
        if (!MatchesAnyPattern(structuralPath, _allowedWhenLocations))
        {
            throw new InvalidOperationException(
                $"'{location}' is not one of the approved expression locations. " +
                "See openspec/specs/cel-policy-model/spec.md for the closed list of locations that may declare 'when'.");
        }

        if (valueNode is not YamlScalarNode whenScalar || IsExplicitNull(whenScalar) || string.IsNullOrEmpty(whenScalar.Value))
        {
            throw new InvalidOperationException(
                $"'{location}' must be a non-empty string when declared.");
        }

        // The composed-policy JSON schema types `when` as a plain string (schema/dependencies.arch.schema.json's
        // expressionField def); this raw pass must reject the same shapes for monolithic policies rather than
        // silently accepting an unquoted boolean/numeric scalar. This is a style/consistency guard, not a
        // fail-closed compilation gap: YamlDotNet deserializes any scalar (quoted or not) to the identical
        // string via its literal text, so `when: 1` already fails CEL compilation with the same TypeMismatch
        // diagnostic as `when: "1"` would - but `when: true` would otherwise compile as the identical, valid
        // (if discouraged - see cel-policy-model's "Broad policy weakening" negative example) `true` literal
        // that `when: "true"` produces, silently diverging from the composed-policy path where the schema
        // rejects the unquoted boolean type outright.
        if (whenScalar.Style == ScalarStyle.Plain && LooksLikeNonStringPlainScalar(whenScalar.Value))
        {
            throw new InvalidOperationException(
                $"'{location}' must be written as a quoted string; an unquoted boolean or numeric literal is not " +
                "a valid 'when' declaration.");
        }
    }

    private static bool LooksLikeNonStringPlainScalar(string value)
    {
        return bool.TryParse(value, out _)
            || long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static bool MatchesAnyPattern(IReadOnlyList<string> structuralPath, string[][] patterns)
    {
        foreach (string[] pattern in patterns)
        {
            if (structuralPath.Count != pattern.Length)
            {
                continue;
            }

            bool matches = true;
            for (int index = 0; index < pattern.Length; index++)
            {
                if (pattern[index] != "*" && !string.Equals(structuralPath[index], pattern[index], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }
}
