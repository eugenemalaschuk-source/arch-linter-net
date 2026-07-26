using System.Globalization;
using System.Reflection;
using System.Text;

namespace ArchLinterNet.Core.Scanning;

// The legacy `declared_api` signature grammar (`<kind> <name>[(<params>)][: <type>]`) is an identity,
// not a description: `public const int Version = 1` and `= 2`, `get;` and `get; set;`, `static` and
// instance, `ref` and `out`, `sealed` and open all normalize to the same string. That is fine for an
// additions-only allowlist but useless for a snapshot that claims to be an *exact* reviewed surface.
//
// This produces a deterministic detail suffix carrying exactly what the base signature drops, so the
// snapshot grammar is `<base signature>[ [<details>]]`. The base signature is left untouched, which
// is what keeps every existing inline `declared_api` policy working unchanged.
internal static class ArchitecturePublicApiSignatureDetails
{
    private const string StaticModifier = "static";
    private const string UnavailableConstant = "<unavailable>";

    public static string Compose(string baseSignature, IReadOnlyList<string> details)
    {
        return details.Count == 0 ? baseSignature : $"{baseSignature} [{string.Join(", ", details)}]";
    }

    // Strips a detail suffix back to the base signature, so a reviewed exact entry can still be
    // correlated with an inline `declared_api` entry written in the legacy grammar.
    public static string StripDetails(string signature)
    {
        if (signature.Length == 0 || signature[^1] != ']')
        {
            return signature;
        }

        int open = signature.LastIndexOf(" [", StringComparison.Ordinal);
        return open < 0 ? signature : signature[..open];
    }

    public static List<string> ForType(Type type)
    {
        List<string> details = new();

        try
        {
            if (type.IsEnum)
            {
                string? underlying = ArchitectureTypeNames.SafeFullName(Enum.GetUnderlyingType(type));
                details.Add($"underlying:{underlying}");
                return details;
            }

            if (type.IsInterface)
            {
                AddGenericConstraints(type, details);
                return details;
            }

            // A C# `static class` is `abstract sealed` in metadata; report it as the source-level
            // concept a reviewer recognizes rather than as two unrelated modifiers.
            if (type is { IsAbstract: true, IsSealed: true })
            {
                details.Add(StaticModifier);
            }
            else
            {
                if (type.IsAbstract)
                {
                    details.Add("abstract");
                }

                if (type.IsSealed && !type.IsValueType)
                {
                    details.Add("sealed");
                }
            }

            if (type.IsValueType && IsReadOnly(type))
            {
                details.Add("readonly");
            }

            AddGenericConstraints(type, details);
        }
        catch (TypeLoadException)
        {
            return details;
        }
        catch (FileNotFoundException)
        {
            return details;
        }

        return details;
    }

    public static List<string> ForMethod(MethodBase method)
    {
        List<string> details = new();

        try
        {
            if (method.IsStatic)
            {
                details.Add(StaticModifier);
            }

            if (method is MethodInfo methodInfo)
            {
                if (methodInfo.IsAbstract)
                {
                    details.Add("abstract");
                }
                else if (methodInfo.IsVirtual && !IsOverride(methodInfo))
                {
                    details.Add("virtual");
                }

                if (IsOverride(methodInfo))
                {
                    details.Add(methodInfo.IsFinal ? "sealed override" : "override");
                }
            }

            AddParameterModifiers(method, details);

            if (method is MethodInfo { IsGenericMethodDefinition: true } generic)
            {
                AddGenericConstraints(generic.GetGenericArguments(), details);
            }
        }
        catch (TypeLoadException)
        {
            return details;
        }
        catch (FileNotFoundException)
        {
            return details;
        }

        return details;
    }

    // Accessor shape is part of the contract a consumer compiles against: adding a setter, or
    // widening a `protected set` to `public set`, changes what callers may do.
    public static List<string> ForProperty(PropertyInfo property)
    {
        List<string> details = new();

        try
        {
            MethodInfo? getter = property.GetMethod;
            MethodInfo? setter = property.SetMethod;

            if ((getter ?? setter)?.IsStatic == true)
            {
                details.Add(StaticModifier);
            }

            if (getter != null)
            {
                details.Add(AccessorToken("get", getter));
            }

            if (setter != null)
            {
                details.Add(AccessorToken(IsInitOnly(setter) ? "init" : "set", setter));
            }
        }
        catch (TypeLoadException)
        {
            return details;
        }
        catch (FileNotFoundException)
        {
            return details;
        }

        return details;
    }

    // A constant's *value* is the API: consumers inline it at compile time, so changing `1` to `2`
    // is a breaking change that leaves the declaration textually identical. Enum members reflect as
    // literal fields, so this is also what makes an enum value change detectable.
    public static List<string> ForField(FieldInfo field)
    {
        List<string> details = new();

        try
        {
            if (field.IsLiteral)
            {
                details.Add($"value:{FormatConstant(field)}");
                return details;
            }

            if (field.IsStatic)
            {
                details.Add(StaticModifier);
            }

            if (field.IsInitOnly)
            {
                details.Add("readonly");
            }
        }
        catch (TypeLoadException)
        {
            return details;
        }
        catch (FileNotFoundException)
        {
            return details;
        }

        return details;
    }

    public static List<string> ForEvent(EventInfo evt)
    {
        List<string> details = new();

        try
        {
            if (evt.AddMethod?.IsStatic == true)
            {
                details.Add(StaticModifier);
            }
        }
        catch (TypeLoadException)
        {
            return details;
        }
        catch (FileNotFoundException)
        {
            return details;
        }

        return details;
    }

    private static string AccessorToken(string name, MethodInfo accessor)
    {
        if (accessor.IsPublic)
        {
            return name;
        }

        if (accessor.IsFamilyOrAssembly)
        {
            return $"{name}:protected internal";
        }

        return accessor.IsFamily ? $"{name}:protected" : $"{name}:internal";
    }

    // The base signature renders `ref`, `out`, and `in` identically as `T&`, so the direction has to
    // be carried here or an `out` turning into a `ref` would be invisible.
    private static void AddParameterModifiers(MethodBase method, List<string> details)
    {
        ParameterInfo[] parameters;
        try
        {
            parameters = method.GetParameters();
        }
        catch (TypeLoadException)
        {
            return;
        }
        catch (FileNotFoundException)
        {
            return;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];
            string? modifier = null;

            if (parameter.ParameterType.IsByRef)
            {
                modifier = ByRefDirection(parameter);
            }
            else if (IsParams(parameter))
            {
                modifier = "params";
            }

            if (modifier != null)
            {
                details.Add($"param{i.ToString(CultureInfo.InvariantCulture)}:{modifier}");
            }
        }
    }

    private static string ByRefDirection(ParameterInfo parameter)
    {
        if (parameter.IsOut)
        {
            return "out";
        }

        return parameter.IsIn ? "in" : "ref";
    }

    private static bool IsParams(ParameterInfo parameter)
    {
        try
        {
            return parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false);
        }
        catch (TypeLoadException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (CustomAttributeFormatException)
        {
            return false;
        }
    }

    private static void AddGenericConstraints(Type type, List<string> details)
    {
        if (type.IsGenericTypeDefinition)
        {
            AddGenericConstraints(type.GetGenericArguments(), details);
        }
    }

    private static void AddGenericConstraints(Type[] genericParameters, List<string> details)
    {
        for (int i = 0; i < genericParameters.Length; i++)
        {
            List<string> constraints = DescribeConstraints(genericParameters[i]);
            if (constraints.Count > 0)
            {
                details.Add($"where{i.ToString(CultureInfo.InvariantCulture)}:{string.Join(" ", constraints)}");
            }
        }
    }

    private static List<string> DescribeConstraints(Type genericParameter)
    {
        List<string> constraints = new();

        try
        {
            GenericParameterAttributes attributes = genericParameter.GenericParameterAttributes;

            if (attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
            {
                constraints.Add("class");
            }

            if (attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            {
                constraints.Add("struct");
            }

            foreach (string constraint in genericParameter.GetGenericParameterConstraints()
                         .Select(ArchitectureTypeNames.SafeFullName)
                         .OrderBy(name => name, StringComparer.Ordinal))
            {
                constraints.Add(constraint);
            }

            // `new()` is implied by (and redundant with) a struct constraint in C# source, so it is
            // only reported when it actually adds information.
            if (attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint)
                && !attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            {
                constraints.Add("new()");
            }
        }
        catch (TypeLoadException)
        {
            return constraints;
        }
        catch (FileNotFoundException)
        {
            return constraints;
        }

        return constraints;
    }

    // Culture-invariant and quoted so a snapshot captured under any locale is byte-identical and a
    // string constant's boundaries stay unambiguous.
    private static string FormatConstant(FieldInfo field)
    {
        object? value;
        try
        {
            value = field.GetRawConstantValue();
        }
        catch (TypeLoadException)
        {
            return UnavailableConstant;
        }
        catch (FileNotFoundException)
        {
            return UnavailableConstant;
        }
        catch (NotSupportedException)
        {
            return UnavailableConstant;
        }
        catch (InvalidOperationException)
        {
            return UnavailableConstant;
        }

        return value switch
        {
            null => "null",
            string text => Quote(text),
            bool flag => flag ? "true" : "false",
            char character => Quote(character.ToString()),
            float single => single.ToString("R", CultureInfo.InvariantCulture),
            double @double => @double.ToString("R", CultureInfo.InvariantCulture),
            decimal @decimal => @decimal.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "null",
        };
    }

    private static string Quote(string text)
    {
        StringBuilder builder = new(text.Length + 2);
        builder.Append('"');
        foreach (char character in text)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    private static bool IsOverride(MethodInfo method)
    {
        try
        {
            return method.IsVirtual && method.GetBaseDefinition().DeclaringType != method.DeclaringType;
        }
        catch (TypeLoadException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private static bool IsInitOnly(MethodInfo setter)
    {
        try
        {
            return setter.ReturnParameter.GetRequiredCustomModifiers()
                .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
        }
        catch (TypeLoadException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private static bool IsReadOnly(Type type)
    {
        try
        {
            return type.GetCustomAttributesData()
                .Any(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
        }
        catch (TypeLoadException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (CustomAttributeFormatException)
        {
            return false;
        }
    }
}
