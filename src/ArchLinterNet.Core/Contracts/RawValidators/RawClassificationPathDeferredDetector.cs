using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// classification.path is schema-accepted but unimplemented (path-convention classification
// depends on issue #171's source/declared-type fact discovery). Detected from the raw node
// tree rather than the deliberately unbound C# model, so declaring it produces a visible,
// deterministic diagnostic instead of pure silence - fires once per policy load, independent of
// scanned types, so it shows up even for a policy with zero scanned types.
//
// Not an IArchitecturePolicyRawDocumentValidator: this runs after deserialization and provenance
// binding (it enriches the loaded document with a notice) rather than as a fail-closed
// pre-deserialization gate, so it keeps its own parse of the effective YAML.
internal static class RawClassificationPathDeferredDetector
{
    public static ArchitectureClassificationPathDeferredNotice? Detect(
        string yaml,
        ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !RawYamlNodes.TryGetMappingChild(root, "classification", out YamlMappingNode? classification)
            || !RawYamlNodes.TryGetChild(classification, "path", out YamlNode? pathNode)
            || pathNode is not YamlSequenceNode pathSequence
            || pathSequence.Children.Count == 0)
        {
            return null;
        }

        string classificationPath = ArchitecturePolicyProvenancePath.AppendProperty(
            ArchitecturePolicyProvenancePath.Property("classification"), "path");
        ArchitecturePolicySourceLocation[] locations = provenance.Nodes
            .Where(entry => ArchitecturePolicyProvenancePath.IsDirectSequenceItem(entry.Key, classificationPath))
            .Select(entry => entry.Value)
            .OrderBy(location => location.SourceOrdinal)
            .ThenBy(location => location.EncounterOrdinal)
            .ToArray();
        return new ArchitectureClassificationPathDeferredNotice(pathSequence.Children.Count)
        {
            PolicyLocations = locations
        };
    }
}
