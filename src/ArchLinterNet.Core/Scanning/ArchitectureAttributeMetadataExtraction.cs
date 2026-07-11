using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ArchLinterNet.Core.Scanning;

// Implements the four fixed metadata-extraction forms (constructor[<index>], property:<Name>,
// const:<Full.Type.NAME>, literal scalar) and canonicalization into the three comparable domains
// (string, bool, decimal), per openspec/specs/attribute-role-extraction/spec.md. Every failure path
// returns (null, reason) rather than throwing — callers omit the key and keep the role assignment.
internal static class ArchitectureAttributeMetadataExtraction
{
    private const string ConstructorPrefix = "constructor[";
    private const string PropertyPrefix = "property:";
    private const string ConstPrefix = "const:";

    public static (object? Canonical, string? FailureReason) Extract(
        object rawYamlValue, CustomAttributeData attributeData, Func<string, Type?> resolveType)
    {
        if (rawYamlValue is string expression)
        {
            if (expression.StartsWith(ConstructorPrefix, StringComparison.Ordinal))
            {
                return ExtractConstructorArgument(expression, attributeData);
            }

            if (expression.StartsWith(PropertyPrefix, StringComparison.Ordinal))
            {
                return ExtractNamedArgument(expression, attributeData);
            }

            if (expression.StartsWith(ConstPrefix, StringComparison.Ordinal))
            {
                return ExtractConstReference(expression, resolveType);
            }
        }

        return TryCanonicalize(rawYamlValue, null) is { } canonical
            ? (canonical, null)
            : (null, $"literal value '{rawYamlValue}' has no representation in the string/boolean/decimal domains");
    }

    private static (object? Canonical, string? FailureReason) ExtractConstructorArgument(
        string expression, CustomAttributeData attributeData)
    {
        if (!expression.EndsWith(']'))
        {
            return (null, $"'{expression}' is not a valid constructor index expression");
        }

        string indexText = expression[ConstructorPrefix.Length..^1];
        if (!int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out int index))
        {
            return (null, $"'{expression}' is not a valid constructor index expression");
        }

        IList<CustomAttributeTypedArgument> arguments = attributeData.ConstructorArguments;
        if (index < 0 || index >= arguments.Count)
        {
            return (null, $"constructor index {index} is out of range (attribute has {arguments.Count} positional arguments)");
        }

        return CanonicalizeTypedArgument(arguments[index]);
    }

    private static (object? Canonical, string? FailureReason) ExtractNamedArgument(
        string expression, CustomAttributeData attributeData)
    {
        string name = expression[PropertyPrefix.Length..];

        CustomAttributeNamedArgument[] matches = attributeData.NamedArguments
            .Where(a => string.Equals(a.MemberName, name, StringComparison.Ordinal))
            .ToArray();

        return matches.Length > 0
            ? CanonicalizeTypedArgument(matches[0].TypedValue)
            : (null, $"named argument '{name}' was not explicitly supplied on this attribute usage");
    }

    private static (object? Canonical, string? FailureReason) ExtractConstReference(
        string expression, Func<string, Type?> resolveType)
    {
        string reference = expression[ConstPrefix.Length..];
        int lastDot = reference.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == reference.Length - 1)
        {
            return (null, $"'{expression}' is not a valid const field reference");
        }

        string typeName = reference[..lastDot];
        string fieldName = reference[(lastDot + 1)..];

        Type? declaringType = resolveType(typeName);
        if (declaringType == null)
        {
            return (null, $"const type '{typeName}' could not be resolved");
        }

        FieldInfo? field = SafeGetField(declaringType, fieldName);
        if (field == null)
        {
            return (null, $"'{expression}' does not name a field on '{typeName}'");
        }

        if (!TryReadConstFieldValue(field, out object? raw))
        {
            return (null, $"'{expression}' is not a compile-time const field");
        }

        return TryCanonicalize(raw, field.FieldType) is { } canonical
            ? (canonical, null)
            : (null, $"const field '{expression}' has no representation in the string/boolean/decimal domains");
    }

    private static FieldInfo? SafeGetField(Type type, string fieldName)
    {
        try
        {
            return type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); // NOSONAR: const resolution must reach non-public compile-time constants
        }
        catch (TypeLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    // C# const decimal fields are not IL literal fields, since decimal has no ECMA constant encoding; // NOSONAR: prose, not commented-out code
    // the compiler instead marks them static initonly and attaches a DecimalConstantAttribute. Both
    // shapes count as compile-time const here; static readonly without DecimalConstantAttribute does not.
    private static bool TryReadConstFieldValue(FieldInfo field, out object? raw)
    {
        if (field.IsLiteral && !field.IsInitOnly)
        {
            raw = field.GetRawConstantValue();
            return true;
        }

        if (field is { IsStatic: true, IsInitOnly: true } && field.GetCustomAttribute<DecimalConstantAttribute>() is { } decimalConstant)
        {
            raw = decimalConstant.Value;
            return true;
        }

        raw = null;
        return false;
    }

    private static (object? Canonical, string? FailureReason) CanonicalizeTypedArgument(CustomAttributeTypedArgument argument)
    {
        return TryCanonicalize(argument.Value, argument.ArgumentType) is { } canonical
            ? (canonical, null)
            : (null, $"value of type '{argument.ArgumentType}' has no representation in the string/boolean/decimal domains");
    }

    // declaredType carries the CLR-declared type of the source (constructor/property argument type, or
    // const field type) so an enum's underlying integral value can be mapped back to its declared member
    // name; literal YAML scalars pass null since YAML alone cannot produce an enum or System.Type value.
    private static object? TryCanonicalize(object? raw, Type? declaredType)
    {
        if (raw == null)
        {
            return null;
        }

        if (declaredType is { IsEnum: true })
        {
            object enumValue;
            try
            {
                enumValue = Enum.ToObject(declaredType, raw);
            }
            catch (ArgumentException)
            {
                return null;
            }

            return CanonicalizeEnum((Enum)enumValue);
        }

        return raw switch
        {
            Enum e => CanonicalizeEnum(e),
            bool b => b,
            string s => s,
            Type t => ArchitectureTypeNames.SafeFullName(t),
            byte or sbyte or short or ushort or int or uint or long or ulong or decimal => TryToDecimal(raw),
            float f => IsFinite(f) ? TryToDecimal(f) : null,
            double d => IsFinite(d) ? TryToDecimal(d) : null,
            _ => null
        };
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static object? TryToDecimal(object raw)
    {
        try
        {
            return Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    // Aliased (two members share one underlying value) or unmapped (no declared member matches) both
    // resolve as a failure — never an arbitrarily-picked name. Compares via direct enum-value equality
    // (not a widening Convert.ToInt64) since an unsigned 64-bit underlying value (e.g. ulong.MaxValue)
    // overflows Int64 and would otherwise throw instead of resolving as a failure.
    private static object? CanonicalizeEnum(Enum value)
    {
        Type enumType = value.GetType();
        string[] names = Enum.GetNames(enumType);
        Array values = Enum.GetValues(enumType);

        string? matchedName = null;
        for (int i = 0; i < names.Length; i++)
        {
            if (!value.Equals(values.GetValue(i)))
            {
                continue;
            }

            if (matchedName != null)
            {
                return null;
            }

            matchedName = names[i];
        }

        return matchedName;
    }
}
