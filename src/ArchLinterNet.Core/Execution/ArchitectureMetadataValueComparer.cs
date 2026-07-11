using System.Globalization;

namespace ArchLinterNet.Core.Execution;

// Shared cross-domain equality/decimal-coercion helpers used by both ArchitectureLayerTypeMatcher
// (exact/AND-only layers.<name>.selector matching, unchanged) and ArchitectureContextSelectorMatcher
// (the broader contextual-selector operator vocabulary). Extracted rather than duplicated per
// add-contextual-dependency-contracts design.md Decision 1.
internal static class ArchitectureMetadataValueComparer
{
    public static bool ValuesEqual(object actual, object expected)
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

    public static bool TryDecimal(object value, out decimal result)
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
