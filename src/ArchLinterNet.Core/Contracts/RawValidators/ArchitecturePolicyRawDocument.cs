using System.Diagnostics.CodeAnalysis;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// The effective policy YAML (root document, or the composed result of an import graph) parsed once
// into the node tree every raw validator walks, paired with the provenance index those validators
// point at the node they are currently checking.
//
// Before extraction each raw check constructed its own YamlStream over the same string; the parse is
// hoisted here so the pipeline pays for it once. The parse still happens at the same semantic point -
// after composition and effective-schema validation, before deserialization - so a malformed-YAML
// failure surfaces from the same stage as before.
internal sealed class ArchitecturePolicyRawDocument
{
    private ArchitecturePolicyRawDocument(YamlMappingNode? root, ArchitecturePolicyProvenanceIndex provenance)
    {
        Root = root;
        Provenance = provenance;
    }

    // Null when the stream carries no document or its root is not a mapping - the case every raw
    // check returned early on before the parse was shared.
    public YamlMappingNode? Root { get; }

    public ArchitecturePolicyProvenanceIndex Provenance { get; }

    public static ArchitecturePolicyRawDocument Parse(string yaml, ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        YamlMappingNode? root = stream.Documents.Count > 0 && stream.Documents[0].RootNode is YamlMappingNode mapping
            ? mapping
            : null;
        return new ArchitecturePolicyRawDocument(root, provenance);
    }

    // Combines the "root is a mapping" and "this top-level block is a mapping" guards the raw checks
    // applied together, so a validator that has nothing to inspect returns on one condition.
    public bool TryGetSection(string key, [NotNullWhen(true)] out YamlMappingNode? section)
    {
        section = null;
        return Root is not null && RawYamlNodes.TryGetMappingChild(Root, key, out section);
    }
}
