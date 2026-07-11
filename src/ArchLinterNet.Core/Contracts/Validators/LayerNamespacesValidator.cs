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

            if (!string.IsNullOrWhiteSpace(layer.NamespaceSuffix)
                && string.IsNullOrWhiteSpace(layer.Namespace))
            {
                throw new InvalidOperationException(
                    $"Layer '{name}' namespace_suffix requires a non-empty namespace.");
            }

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
