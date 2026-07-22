using System.Globalization;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class LayerNamespacesValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach ((string name, ArchitectureLayer layer) in document.Layers)
        {
            document.Provenance.SetValidationSubject(layer);
            if (string.IsNullOrWhiteSpace(layer.Namespace) && layer.Selector == null)
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' must declare a non-empty namespace or selector.");
            }

            if (!string.IsNullOrWhiteSpace(layer.NamespaceSuffix)
                && string.IsNullOrWhiteSpace(layer.Namespace))
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' namespace_suffix requires a non-empty namespace.");
            }

            if (layer.Exclude.Count > 0 && string.IsNullOrWhiteSpace(layer.Namespace))
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' exclude requires a non-empty namespace: a selector-only layer " +
                    "has no namespace-matched scope for exclude entries to subtract from.");
            }

            ValidateExclude(document, name, layer);

            if (layer.Selector == null)
            {
                _ = layer.GlobPattern;
                continue;
            }

            if (layer.Selector.Metadata == null)
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' selector metadata must be an object when declared.");
            }

            if (string.IsNullOrWhiteSpace(layer.Selector.Role))
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' selector must declare a non-empty role.");
            }

            if (layer.Selector.Metadata.Keys.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' selector metadata keys must be non-empty.");
            }

            foreach ((string key, object value) in layer.Selector.Metadata)
            {
                if (value is string stringValue && stringValue.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Layer '{name}' selector metadata key '{key}' must not be an empty string.");
                }

                if (!IsSupportedScalar(value))
                {
                    throw new InvalidOperationException(
                        $"Layer '{name}' selector metadata key '{key}' must be a string, boolean, or finite numeric scalar.");
                }
            }

            if (!string.IsNullOrWhiteSpace(layer.Namespace))
            {
                _ = layer.GlobPattern;
            }
        }
    }

    // Eagerly validates every exclude entry's namespace/glob at load time (mirroring the eager
    // `_ = layer.GlobPattern` check above for the layer's own namespace) instead of leaving a
    // malformed entry to surface only when a scanned type happens to reach it during matching -
    // or never surface at all, silently no-opping. Also resolves and stores each entry's exact
    // source location so JSON/SARIF/Testing API diagnostics (e.g. unmatched-layer-exclusion) can
    // name the precise `layers.<name>.exclude[<index>]` element instead of only the owning layer.
    private static void ValidateExclude(ArchitectureContractDocument document, string layerName, ArchitectureLayer layer)
    {
        string excludeListPath = ArchitecturePolicyProvenancePath.AppendProperty(
            ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property("layers"), layerName),
            "exclude");

        for (int index = 0; index < layer.Exclude.Count; index++)
        {
            ArchitectureLayerExclusion exclusion = layer.Exclude[index];
            string exclusionPath = ArchitecturePolicyProvenancePath.AppendIndex(excludeListPath, index);
            document.Provenance.SetValidationSubject(exclusionPath);

            if (string.IsNullOrWhiteSpace(exclusion.Namespace))
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' exclude entry {index} must declare a non-empty namespace.");
            }

            try
            {
                _ = exclusion.GlobPattern;
            }
            catch (InvalidNamespacePatternException ex)
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' exclude entry {index}: {ex.Message}", ex);
            }

            document.Provenance.TryGetLocation(exclusionPath, out ArchitecturePolicySourceLocation? location);
            exclusion.PolicyLocation = location;
        }
    }

    private static bool IsSupportedScalar(object? value)
    {
        if (value is null or string or bool)
        {
            return value is not null;
        }

        return value switch
        {
            byte or sbyte or short or ushort or int or uint or long or ulong or decimal => true,
            float f => InDecimalRange(f),
            double d => InDecimalRange(d),
            _ => TryConvertibleFiniteNumber(value)
        };
    }

    private static bool InDecimalRange(double value)
    {
        try
        {
            _ = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryConvertibleFiniteNumber(object value)
    {
        try
        {
            _ = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }
}
