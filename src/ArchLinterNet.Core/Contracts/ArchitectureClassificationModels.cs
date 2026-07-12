using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

// Binds classification.attributes/classification.assembly_attributes/classification.inheritance/
// classification.namespace and the subset of classification.precedence relevant to them. Other
// classification.* fields (path, overrides, exclusions) are intentionally unbound:
// ArchitecturePolicyDocumentLoader's deserializer ignores unmatched properties, so those sections
// remain schema-valid no-ops until their own implementation issues land.
public sealed class ArchitectureClassificationConfiguration
{
    // Null means "not declared" (schema default: every source enabled). When declared, a source
    // omitted from this list is disabled, per the reviewed schema's precedence semantics.
    [YamlMember(Alias = "precedence")]
    public List<string>? Precedence { get; set; }

    [YamlMember(Alias = "attributes")]
    public List<ArchitectureAttributeClassificationMapping> Attributes { get; set; } = new();

    [YamlMember(Alias = "assembly_attributes")]
    public List<ArchitectureAttributeClassificationMapping> AssemblyAttributes { get; set; } = new();

    [YamlMember(Alias = "inheritance")]
    public List<ArchitectureInheritanceClassificationMapping> Inheritance { get; set; } = new();

    [YamlMember(Alias = "namespace")]
    public List<ArchitectureNamespaceClassificationMapping> Namespace { get; set; } = new();

    public bool IsSourceEnabled(string sourceName)
    {
        return Precedence == null || Precedence.Contains(sourceName, StringComparer.Ordinal);
    }
}

public sealed class ArchitectureAttributeClassificationMapping
{
    [YamlMember(Alias = "attribute")] public string Attribute { get; set; } = string.Empty;

    [YamlMember(Alias = "role")] public string Role { get; set; } = string.Empty;

    // Raw YAML scalar per key: a string is checked against the constructor[/property:/const: prefixes at
    // extraction time; any other scalar (bool/number) is already a literal value in its final domain.
    [YamlMember(Alias = "metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public sealed class ArchitectureInheritanceClassificationMapping
{
    [YamlMember(Alias = "base_type")] public string BaseType { get; set; } = string.Empty;

    [YamlMember(Alias = "role")] public string Role { get; set; } = string.Empty;

    // Restricted at extraction time to literal scalar or const:<Full.Type.NAME> only — inheritance
    // evidence carries no constructor/property arguments, since there is no attribute instance.
    [YamlMember(Alias = "metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public sealed class ArchitectureNamespaceClassificationMapping
{
    [YamlMember(Alias = "namespace")] public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "role")] public string Role { get; set; } = string.Empty;

    // Restricted at extraction time to literal scalar or const:<Full.Type.NAME> only, same as inheritance.
    [YamlMember(Alias = "metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}
