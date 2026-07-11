using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

// Binds only classification.attributes/classification.assembly_attributes — the attribute-role-extraction
// capability's scope. Other classification.* fields (precedence, inheritance, namespace, path, overrides,
// exclusions) are intentionally unbound: ArchitecturePolicyDocumentLoader's deserializer ignores unmatched
// properties, so those sections remain schema-valid no-ops until their own implementation issues land.
public sealed class ArchitectureClassificationConfiguration
{
    [YamlMember(Alias = "attributes")]
    public List<ArchitectureAttributeClassificationMapping> Attributes { get; set; } = new();

    [YamlMember(Alias = "assembly_attributes")]
    public List<ArchitectureAttributeClassificationMapping> AssemblyAttributes { get; set; } = new();
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
