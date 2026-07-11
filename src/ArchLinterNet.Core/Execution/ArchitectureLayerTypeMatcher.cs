using System.Globalization;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal static class ArchitectureLayerTypeMatcher
{
    public static bool Matches(ArchitectureLayer layer, Type type, ArchitectureRoleIndex roleIndex)
    {
        if (!string.IsNullOrWhiteSpace(layer.Namespace)
            && !Resolution.ArchitectureLayerResolver.MatchesNamespace(
                layer, ArchitectureTypeNames.SafeNamespace(type)))
        {
            return false;
        }

        if (layer.Selector == null)
        {
            return !string.IsNullOrWhiteSpace(layer.Namespace);
        }

        if (!roleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor)
            || !string.Equals(descriptor.Role, layer.Selector.Role, StringComparison.Ordinal))
        {
            return false;
        }

        return layer.Selector.Metadata.All(entry =>
            descriptor.Metadata.TryGetValue(entry.Key, out object? actual)
            && ValuesEqual(actual, entry.Value));
    }

    private static bool ValuesEqual(object actual, object expected)
    {
        if (actual is string actualString && expected is string expectedString)
        {
            return string.Equals(actualString, expectedString, StringComparison.Ordinal);
        }

        if (actual is bool actualBool && expected is bool expectedBool)
        {
            return actualBool == expectedBool;
        }

        if (TryDecimal(actual, out decimal actualDecimal)
            && TryDecimal(expected, out decimal expectedDecimal))
        {
            return actualDecimal == expectedDecimal;
        }

        return Equals(actual, expected);
    }

    private static bool TryDecimal(object value, out decimal result)
    {
        if (value is not byte and not sbyte and not short and not ushort and not int and not uint and not long and not ulong
            and not float and not double and not decimal)
        {
            result = default;
            return false;
        }

        try
        {
            if (value is float f && (float.IsNaN(f) || float.IsInfinity(f))
                || value is double d && (double.IsNaN(d) || double.IsInfinity(d)))
            {
                result = default;
                return false;
            }

            result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
        {
            result = default;
            return false;
        }
    }
}
