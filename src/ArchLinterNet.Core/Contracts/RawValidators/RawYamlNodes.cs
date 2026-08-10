using System.Diagnostics.CodeAnalysis;
using ArchLinterNet.Core.Contracts.PolicyImports;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.RawValidators;

// Shared raw-node vocabulary for the pre-deserialization validation pipeline: the schema key names
// several capabilities agree on, node navigation, contract naming, contract provenance paths, and
// the known-key check. Extracted verbatim from ArchitecturePolicyDocumentLoader so the message text
// (which invalid-policy tests assert on) stays byte-identical.
internal static class RawYamlNodes
{
    public const string MetadataKey = "metadata";
    public const string SourceKey = "source";
    public const string ForbiddenKey = "forbidden";
    public const string WhenKey = "when";
    public const string ContractsKey = "contracts";
    public const string UnnamedContractName = "<unnamed>";
    public const string NamespaceSuffixKey = "namespace_suffix";
    public const string ExcludeKey = "exclude";
    public const string OverlapsWithKey = "overlaps_with";

    public static bool TryGetChild(YamlMappingNode parent, string key, [NotNullWhen(true)] out YamlNode? child)
    {
        foreach ((YamlNode candidateKey, YamlNode candidateValue) in parent.Children)
        {
            if (candidateKey is YamlScalarNode scalar
                && string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                child = candidateValue;
                return true;
            }
        }

        child = null;
        return false;
    }

    public static bool TryGetMappingChild(YamlMappingNode parent, string key, [NotNullWhen(true)] out YamlMappingNode? child)
    {
        child = null;
        if (!TryGetChild(parent, key, out YamlNode? node) || node is not YamlMappingNode mapping)
        {
            return false;
        }

        child = mapping;
        return true;
    }

    public static bool TryGetNonNullChild(YamlMappingNode parent, string key, out YamlNode? child)
    {
        return TryGetChild(parent, key, out child) && !IsExplicitNull(child);
    }

    public static bool IsExplicitNull(YamlNode? node)
    {
        return node is YamlScalarNode scalar
            && (scalar.Value is null
                || (scalar.Style == ScalarStyle.Plain
                    && string.Equals(scalar.Value, "null", StringComparison.OrdinalIgnoreCase))
                || (scalar.Style == ScalarStyle.Plain
                    && string.Equals(scalar.Value, "~", StringComparison.Ordinal)));
    }

    // A contract's authored name, or the placeholder used in diagnostics for a contract that has not
    // declared one - the same fallback every raw contract check applied before extraction.
    public static string ContractName(YamlMappingNode contractNode)
    {
        return TryGetChild(contractNode, "name", out YamlNode? nameNode) && nameNode is YamlScalarNode nameScalar
            ? nameScalar.Value ?? UnnamedContractName
            : UnnamedContractName;
    }

    // The one place a raw validator walks a `contracts.<group>` sequence. Pointing the provenance
    // validation subject at the indexed contract before each callback is load-bearing (it is what
    // carries the authored/imported location into a raw diagnostic), so it lives here rather than
    // being repeated - and re-derived - by every contract-family validator. Non-mapping entries are
    // skipped, matching the behavior each validator had before this loop was shared.
    public static void ForEachContract(
        ArchitecturePolicyRawDocument document,
        string groupKey,
        Action<YamlMappingNode, string, int> validateContract)
    {
        if (!document.TryGetSection(ContractsKey, out YamlMappingNode? contracts)
            || !TryGetChild(contracts, groupKey, out YamlNode? groupNode)
            || groupNode is not YamlSequenceNode sequence)
        {
            return;
        }

        for (int index = 0; index < sequence.Children.Count; index++)
        {
            if (sequence.Children[index] is not YamlMappingNode contractNode)
            {
                continue;
            }

            document.Provenance.SetValidationSubject(ContractPath(groupKey, index));
            validateContract(contractNode, groupKey, index);
        }
    }

    public static string ContractPath(string groupKey, int index)
    {
        return ArchitecturePolicyProvenancePath.AppendIndex(
            ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property(ContractsKey), groupKey),
            index);
    }

    public static void ValidateKnownKeys(YamlMappingNode node, string contractName, string location, IEnumerable<string> allowed)
    {
        foreach ((YamlNode keyNode, _) in node.Children)
        {
            if (keyNode is YamlScalarNode scalar && !allowed.Contains(scalar.Value, StringComparer.Ordinal))
                throw new InvalidOperationException($"Contextual contract '{contractName}' declares an unknown property '{scalar.Value}' on {location}.");
        }
    }
}
