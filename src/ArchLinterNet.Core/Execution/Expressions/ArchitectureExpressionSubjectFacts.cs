namespace ArchLinterNet.Core.Execution.Expressions;

// Shaped exactly like the closed "shared subject object" in openspec/specs/cel-policy-model/spec.md
// - the complete first-wave member list for the `subject`/`source`/`target` CEL contexts. Populating
// this record from real Core architecture facts (declared-type facts, classification results,
// reflection, IL scanning) is #164's concern; this type is the CEL-value-construction boundary
// Core owns. See openspec/changes/core-cel-integration/design.md Decision D5.
internal sealed record ArchitectureExpressionSubjectFacts(
    string FullName,
    string SimpleName,
    string Namespace,
    string AssemblyName,
    string ProjectName,
    string Role,
    IReadOnlyDictionary<string, string> MetadataText,
    IReadOnlyDictionary<string, bool> MetadataBool,
    string Kind,
    bool IsAbstract,
    bool IsSealed,
    IReadOnlyList<string> BaseTypeNames,
    IReadOnlyList<string> InterfaceTypeNames,
    IReadOnlyList<string> AttributeTypeNames,
    IReadOnlyList<string> SourcePaths,
    IReadOnlyList<string> SourceDirectoryPrefixes);
