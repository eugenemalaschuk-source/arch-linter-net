using ArchLinterNet.Core.Contracts.PolicyImports;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// Ordinary policy loading intentionally ignores unmatched YamlDotNet properties. Metrics are a
// closed configuration boundary, so keep unknown keys and malformed collection shapes on the
// typed policy-error path instead of silently changing a measurement's meaning.
internal sealed class RawMetricDefinitionValidator : IArchitecturePolicyRawDocumentValidator
{
    private static readonly string[] _definitionKeys =
        ["id", "kind", "topology_node", "unit", "public_api_surface"];

    public void Validate(ArchitecturePolicyRawDocument document)
    {
        if (document.Root is null || !RawYamlNodes.TryGetChild(document.Root, "metrics", out YamlNode? metricsNode))
        {
            return;
        }

        document.Provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.Property("metrics"));
        if (metricsNode is not YamlSequenceNode metrics)
        {
            throw new InvalidOperationException("Policy 'metrics' must be a list of objects.");
        }

        string metricsPath = ArchitecturePolicyProvenancePath.Property("metrics");
        for (int index = 0; index < metrics.Children.Count; index++)
        {
            document.Provenance.SetValidationSubject(
                ArchitecturePolicyProvenancePath.AppendIndex(metricsPath, index));
            if (metrics.Children[index] is not YamlMappingNode definition)
            {
                throw new InvalidOperationException($"Metric definition {index} must be an object.");
            }

            foreach ((YamlNode key, _) in definition.Children)
            {
                if (key is YamlScalarNode scalar
                    && !_definitionKeys.Contains(scalar.Value, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Metric definition {index} contains unknown property '{scalar.Value}'.");
                }
            }
        }
    }
}
