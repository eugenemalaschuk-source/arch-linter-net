using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Approval test for the whole public API contract of <c>ArchLinterNet.Core</c>, mirroring the
/// <c>ArchLinterNet.CEL</c> one (#329): every public and public-nested type with its base
/// type/interfaces, constructors, fields, properties, events, operators and methods, with
/// namespace-qualified type names, nullable annotations and named tuple elements. Compares against
/// the approved baseline in <c>ApprovedApi/ArchLinterNet.Core.approved.txt</c>; any addition,
/// removal or signature change fails with a readable diff instead of shipping silently.
///
/// Added after PR #420, where an internal-by-intent member was written as <c>public</c> on
/// <see cref="ArchitectureViolation"/> — a type callers receive from
/// <c>ArchitectureValidationResult.Violations</c> — and every gate stayed green because Core, unlike
/// CEL, had no such test. Update the baseline deliberately when the public API is meant to change.
/// </summary>
[TestFixture]
public sealed class CorePublicApiSurfaceApprovalTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                                        BindingFlags.DeclaredOnly;

    private static readonly NullabilityInfoContext _nullabilityCtx = new();

    private static readonly Dictionary<System.Type, string> _aliases = new()
    {
        [typeof(void)] = "void",
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(char)] = "char",
        [typeof(string)] = "string",
        [typeof(object)] = "object",
    };

    [Test]
    public void PublicApiSurface_MatchesApprovedBaseline()
    {
        string actual = DescribeSurface(typeof(ArchitectureViolation).Assembly);
        string approvedPath = ApprovedBaselinePath();

        Assert.That(File.Exists(approvedPath), Is.True, $"approved baseline missing at {approvedPath}");
        Assert.That(actual.Replace("\r\n", "\n"), Is.EqualTo(File.ReadAllText(approvedPath).Replace("\r\n", "\n")));
    }

    private static string DescribeSurface(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t.IsPublic || t.IsNestedPublic)
            .OrderBy(QualifiedName, System.StringComparer.Ordinal);

        var builder = new StringBuilder();
        foreach (var type in types)
        {
            var suffix = type.IsEnum
                ? " : " + DescribeType(System.Enum.GetUnderlyingType(type), null)
                : DescribeBaseTypesAndInterfaces(type);
            builder.Append(DescribeTypeKind(type)).Append(' ').Append(QualifiedName(type))
                .Append(DescribeGenericParams(type.IsGenericTypeDefinition ? type.GetGenericArguments() : []))
                .Append(suffix)
                .Append('\n');
            foreach (var member in DescribeMembers(type))
                builder.Append("  ").Append(member).Append('\n');
        }

        return builder.ToString();
    }

    private static string DescribeTypeKind(System.Type type)
    {
        if (type.IsInterface) return "interface";
        if (type.IsEnum) return "enum";
        if (type.IsValueType)
        {
            var modifiers = "";
            if (IsReadOnlyStruct(type)) modifiers += "readonly ";
            if (type.IsByRefLike) modifiers += "ref ";
            return $"{modifiers}struct";
        }

        if (typeof(System.Delegate).IsAssignableFrom(type)) return "delegate";
        if (type.IsAbstract && type.IsSealed) return "static class";
        if (type.IsSealed) return "sealed class";
        if (type.IsAbstract) return "abstract class";
        return "class";
    }

    private static bool IsReadOnlyStruct(System.Type type) =>
        type.GetCustomAttributesData().Any(a => a.AttributeType == typeof(IsReadOnlyAttribute));

    private static string DescribeBaseTypesAndInterfaces(System.Type type)
    {
        var parts = new List<string>();
        if (type.BaseType is { } baseType &&
            baseType != typeof(object) && baseType != typeof(System.ValueType) && baseType != typeof(System.Enum))
            parts.Add(DescribeType(baseType, null));

        var baseInterfaces = type.BaseType?.GetInterfaces() ?? [];
        var declaredInterfaces = type.GetInterfaces()
            .Except(baseInterfaces)
            .OrderBy(QualifiedName, System.StringComparer.Ordinal)
            .Select(i => DescribeType(i, null));
        parts.AddRange(declaredInterfaces);

        return parts.Count == 0 ? "" : " : " + string.Join(", ", parts);
    }

    private static IEnumerable<string> DescribeMembers(System.Type type)
    {
        if (type.IsEnum)
        {
            var underlying = System.Enum.GetUnderlyingType(type);
            foreach (var name in System.Enum.GetNames(type).OrderBy(n => n, System.StringComparer.Ordinal))
            {
                var value = System.Convert.ChangeType(System.Enum.Parse(type, name), underlying, CultureInfo.InvariantCulture);
                yield return $"enum-member {name} = {value}";
            }

            yield break;
        }

        var descriptions = new List<string>();

        foreach (var ctor in type.GetConstructors(Flags))
            descriptions.Add($"ctor({DescribeParams(ctor.GetParameters())})");

        foreach (var field in type.GetFields(Flags))
            descriptions.Add(DescribeField(field));

        var accessorMethods = new HashSet<MethodInfo>();
        foreach (var prop in type.GetProperties(Flags))
        {
            var get = prop.GetGetMethod(true);
            var set = prop.GetSetMethod(true);
            if (get is not null) accessorMethods.Add(get);
            if (set is not null) accessorMethods.Add(set);
            descriptions.Add(DescribeProperty(prop, get, set));
        }

        foreach (var evt in type.GetEvents(Flags))
        {
            var add = evt.GetAddMethod(true);
            var remove = evt.GetRemoveMethod(true);
            if (add is not null) accessorMethods.Add(add);
            if (remove is not null) accessorMethods.Add(remove);
            var isStatic = (add ?? remove)?.IsStatic == true;
            descriptions.Add($"event {(isStatic ? "static " : "")}{DescribeType(evt.EventHandlerType!, null)} {evt.Name}");
        }

        foreach (var method in type.GetMethods(Flags).Where(m => !accessorMethods.Contains(m)))
        {
            if (method.IsSpecialName && method.Name.StartsWith("op_", System.StringComparison.Ordinal))
            {
                descriptions.Add(DescribeOperator(method));
                continue;
            }

            var isStatic = method.IsStatic ? "static " : "";
            var generic = method.IsGenericMethodDefinition ? DescribeGenericParams(method.GetGenericArguments()) : "";
            var returnType = DescribeType(method.ReturnType, _nullabilityCtx.Create(method.ReturnParameter), NewCursor(method.ReturnParameter));
            descriptions.Add($"method {isStatic}{returnType} {method.Name}{generic}({DescribeParams(method.GetParameters())})");
        }

        foreach (var op in descriptions.OrderBy(d => d, System.StringComparer.Ordinal))
            yield return op;
    }

    private static string DescribeField(FieldInfo field)
    {
        var modifiers = FieldModifiers(field);
        var typeName = DescribeType(field.FieldType, _nullabilityCtx.Create(field), NewCursor(field));
        var constSuffix = field.IsLiteral ? $" = {FormatConstant(field.GetRawConstantValue())}" : "";
        return $"field {modifiers}{typeName} {field.Name}{constSuffix}";
    }

    private static string FieldModifiers(FieldInfo field)
    {
        if (field.IsLiteral) return "const ";
        var modifiers = new List<string>();
        if (field.IsStatic) modifiers.Add("static");
        if (field.IsInitOnly) modifiers.Add("readonly");
        return modifiers.Count == 0 ? "" : string.Join(" ", modifiers) + " ";
    }

    private static string DescribeProperty(PropertyInfo prop, MethodInfo? get, MethodInfo? set)
    {
        var isStatic = (get ?? set)?.IsStatic == true;
        var indexParams = prop.GetIndexParameters();
        var name = indexParams.Length > 0 ? $"this[{DescribeParams(indexParams)}]" : prop.Name;
        var typeName = DescribeType(prop.PropertyType, _nullabilityCtx.Create(prop), NewCursor(prop));
        var accessors = DescribeAccessor(get, "get") + DescribeAccessor(set, IsInitOnly(set) ? "init" : "set");
        return $"property {(isStatic ? "static " : "")}{typeName} {name} {{{accessors}}}";
    }

    private static string DescribeAccessor(MethodInfo? accessor, string keyword)
    {
        if (accessor is null) return "";
        var visibility = AccessorVisibility(accessor);
        var visibilityPrefix = visibility == "public" ? "" : visibility + " ";
        return $"{visibilityPrefix}{keyword};";
    }

    private static string AccessorVisibility(MethodInfo m)
    {
        if (m.IsPublic) return "public";
        if (m.IsFamilyOrAssembly) return "protected internal";
        if (m.IsFamilyAndAssembly) return "private protected";
        if (m.IsFamily) return "protected";
        if (m.IsAssembly) return "internal";
        return "private";
    }

    private static bool IsInitOnly(MethodInfo? m) =>
        m is not null && m.ReturnParameter.GetRequiredCustomModifiers()
            .Any(t => t == typeof(IsExternalInit));

    private static string DescribeOperator(MethodInfo method)
    {
        if (method.Name is "op_Implicit" or "op_Explicit")
        {
            var kind = method.Name == "op_Implicit" ? "implicit" : "explicit";
            var returnType = DescribeType(method.ReturnType, _nullabilityCtx.Create(method.ReturnParameter));
            return $"{kind} operator {returnType}({DescribeParams(method.GetParameters())})";
        }

        var symbol = OperatorSymbol(method.Name);
        return $"operator {symbol}({DescribeParams(method.GetParameters())})";
    }

    private static string OperatorSymbol(string name) => name switch
    {
        "op_Equality" => "==",
        "op_Inequality" => "!=",
        "op_LessThan" => "<",
        "op_GreaterThan" => ">",
        "op_LessThanOrEqual" => "<=",
        "op_GreaterThanOrEqual" => ">=",
        "op_Addition" => "+",
        "op_Subtraction" => "-",
        "op_Multiply" => "*",
        "op_Division" => "/",
        "op_Modulus" => "%",
        "op_UnaryNegation" => "-(unary)",
        "op_UnaryPlus" => "+(unary)",
        "op_LogicalNot" => "!",
        "op_OnesComplement" => "~",
        "op_True" => "true",
        "op_False" => "false",
        "op_Increment" => "++",
        "op_Decrement" => "--",
        _ => name,
    };

    private static string DescribeParams(ParameterInfo[] parameters) =>
        string.Join(", ", parameters.Select(DescribeParam));

    private static string DescribeParam(ParameterInfo p)
    {
        var isByRef = p.ParameterType.IsByRef;
        var refModifier = isByRef
            ? (p.IsOut ? "out " : (p.IsIn ? "in " : "ref "))
            : "";
        var isParams = p.IsDefined(typeof(System.ParamArrayAttribute), false) ? "params " : "";
        var effectiveType = isByRef ? p.ParameterType.GetElementType()! : p.ParameterType;
        var typeName = DescribeType(effectiveType, _nullabilityCtx.Create(p), NewCursor(p));
        var defaultSuffix = p.HasDefaultValue ? $" = {FormatConstant(p.DefaultValue)}" : "";
        return $"{refModifier}{isParams}{typeName} {p.Name}{defaultSuffix}";
    }

    private static string FormatConstant(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        System.Enum e => e.ToString(),
        _ => System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null",
    };

    private static string DescribeGenericParams(System.Type[] typeArgs)
    {
        if (typeArgs.Length == 0) return "";
        return $"<{string.Join(",", typeArgs.Select(DescribeGenericParam))}>";
    }

    private static string DescribeGenericParam(System.Type t)
    {
        var constraints = new List<string>();
        var attrs = t.GenericParameterAttributes;
        if (attrs.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint)) constraints.Add("class");
        if (attrs.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint)) constraints.Add("struct");
        foreach (var c in t.GetGenericParameterConstraints())
            if (c != typeof(System.ValueType)) constraints.Add(DescribeType(c, null));
        if (attrs.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) &&
            !attrs.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            constraints.Add("new()");
        return constraints.Count == 0 ? t.Name : $"{t.Name} where {t.Name} : {string.Join(",", constraints)}";
    }

    /// <summary>
    /// Walks a flat <see cref="TupleElementNamesAttribute.TransformNames"/> array in the same
    /// pre-order that the C# compiler assigns it: one slot consumed per named-tuple element
    /// encountered while descending through the member's full type tree.
    /// </summary>
    private sealed class TupleNameCursor
    {
        private readonly string?[] _names;
        private int _index;

        public TupleNameCursor(string?[] names) => _names = names;

        public string? Next() => _index < _names.Length ? _names[_index++] : null;
    }

    private static TupleNameCursor? NewCursor(ICustomAttributeProvider provider)
    {
        var attr = provider.GetCustomAttributes(typeof(TupleElementNamesAttribute), false)
            .Cast<TupleElementNamesAttribute>().FirstOrDefault();
        return attr is null ? null : new TupleNameCursor(attr.TransformNames.ToArray());
    }

    private static bool IsValueTuple(System.Type type) =>
        type.IsGenericType && type.FullName?.StartsWith("System.ValueTuple`", System.StringComparison.Ordinal) == true;

    private static string DescribeType(System.Type type, NullabilityInfo? info, TupleNameCursor? cursor = null)
    {
        if (System.Nullable.GetUnderlyingType(type) is { } underlying)
        {
            var underlyingInfo = info?.GenericTypeArguments.ElementAtOrDefault(0);
            return DescribeType(underlying, underlyingInfo, cursor) + "?";
        }

        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var elementName = DescribeType(elementType, info?.ElementType, cursor);
            return $"{elementName}[]{NullSuffix(type, info)}";
        }

        if (IsValueTuple(type))
        {
            var args = type.GetGenericArguments();
            var parts = new List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                var name = cursor?.Next();
                var argInfo = info?.GenericTypeArguments.ElementAtOrDefault(i);
                var argText = DescribeType(args[i], argInfo, cursor);
                parts.Add(name is null ? argText : $"{argText} {name}");
            }

            return $"({string.Join(", ", parts)})";
        }

        if (type.IsGenericType)
        {
            var baseName = QualifiedName(type.GetGenericTypeDefinition());
            var args = type.GetGenericArguments();
            var parts = new List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                var argInfo = info?.GenericTypeArguments.ElementAtOrDefault(i);
                parts.Add(DescribeType(args[i], argInfo, cursor));
            }

            return $"{baseName}<{string.Join(",", parts)}>{NullSuffix(type, info)}";
        }

        var alias = _aliases.TryGetValue(type, out var a) ? a : QualifiedName(type);
        return $"{alias}{NullSuffix(type, info)}";
    }

    private static string NullSuffix(System.Type type, NullabilityInfo? info)
    {
        if (type.IsValueType || info is null) return "";
        return info.ReadState == NullabilityState.Nullable ? "?" : "";
    }

    private static string QualifiedName(System.Type type)
    {
        if (type.IsGenericParameter) return type.Name;
        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0) name = name[..tick];
        var prefix = type.IsNested
            ? QualifiedName(type.DeclaringType!) + "."
            : type.Namespace is null ? "" : type.Namespace + ".";
        return prefix + name;
    }

    // The baseline lives next to the test source rather than inline: Core's surface is an order of
    // magnitude larger than CEL's, so an inline constant would dominate the file and bury the diff
    // that makes an unintended API change obvious in review.
    private static string ApprovedBaselinePath()
    {
        return Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "ApprovedApi",
            "ArchLinterNet.Core.approved.txt");
    }
}
