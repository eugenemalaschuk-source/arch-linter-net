using System.Globalization;
using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class LayerNamespacesValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach ((string name, ArchitectureLayer layer) in document.Layers)
        {
            if (string.IsNullOrWhiteSpace(layer.Namespace) && layer.Selector == null)
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' must declare a non-empty namespace or selector.");
            }

            if (layer.Selector == null)
            {
                _ = layer.GlobPattern;
                continue;
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

    private static bool IsSupportedScalar(object? value)
    {
        if (value is null or string or bool)
        {
            return value is not null;
        }

        return value switch
        {
            byte or sbyte or short or ushort or int or uint or long or ulong or decimal => true,
            float f => !float.IsNaN(f) && !float.IsInfinity(f),
            double d => !double.IsNaN(d) && !double.IsInfinity(d),
            _ => TryConvertibleFiniteNumber(value)
        };
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
