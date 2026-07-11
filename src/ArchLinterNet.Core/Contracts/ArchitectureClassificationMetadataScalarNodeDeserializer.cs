using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts;

// YamlDotNet's default scalar handling for an `object`-typed target (e.g. Dictionary<string, object>
// Metadata) returns the raw string for every scalar, regardless of whether it was written unquoted as
// `true`/`1`/`1.5` or quoted as a string — so classification.attributes[].metadata literal booleans and
// numbers would otherwise always canonicalize into the string domain instead of boolean/decimal, per
// openspec/specs/attribute-role-extraction's "Metadata values are canonicalized..." requirement.
// This mirrors the JSON schema's scalarValue union (string/boolean/number) by inferring the CLR type
// from the scalar's plain (unquoted) style; a quoted scalar always stays a string.
internal sealed class ArchitectureClassificationMetadataScalarNodeDeserializer : INodeDeserializer
{
    public bool Deserialize(
        IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer,
        out object? value, ObjectDeserializer rootDeserializer)
    {
        if (expectedType != typeof(object) || !reader.Accept<Scalar>(out Scalar? scalar))
        {
            value = null;
            return false;
        }

        reader.MoveNext();
        value = scalar!.Style == ScalarStyle.Plain ? InferPlainScalarValue(scalar.Value) : scalar.Value;
        return true;
    }

    private static object InferPlainScalarValue(string raw)
    {
        if (bool.TryParse(raw, out bool boolValue))
        {
            return boolValue;
        }

        if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long longValue))
        {
            return longValue;
        }

        // Try decimal before double: decimal holds ~28-29 significant digits exactly, so a literal
        // like `1.23456789012345678901` round-trips exactly here, whereas parsing it as a double
        // first (double has ~15-17 significant digits) would silently lose precision before the
        // extraction engine ever canonicalizes it into the decimal domain. double remains the
        // fallback for magnitudes outside decimal's range (e.g. `1e300`) or non-finite literals.
        if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalValue))
        {
            return decimalValue;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
        {
            return doubleValue;
        }

        return raw;
    }
}
