using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using ArchLinterNet.CEL;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Approval test (#329) that enumerates the entire public API contract of
/// <c>ArchLinterNet.CEL</c> — every public and public-nested type with its base type/interfaces,
/// constructors, fields (modifiers and constant values), properties (including indexers and
/// asymmetric accessor visibility/init-only setters), events, operators/conversions, and methods
/// (static-ness, generic constraints, ref/out/in/params/default parameters) — with fully
/// namespace-qualified type names, recursive nullable-reference annotations (including through
/// generic arguments and arrays), and named tuple elements. Compares it against an approved
/// baseline; any addition, removal, or signature change to the public contract fails this test
/// with a readable diff, instead of silently shipping an API change. Update
/// <see cref="ApprovedSurface"/> deliberately when the public API intentionally changes.
/// </summary>
[TestFixture]
public sealed class CelPublicApiSurfaceApprovalTests
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
        var actual = DescribeSurface(typeof(CelEnvironment).Assembly);
        Assert.That(actual, Is.EqualTo(ApprovedSurface));
    }

    private static string DescribeSurface(Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t.IsPublic || t.IsNestedPublic)
            .OrderBy(QualifiedName, System.StringComparer.Ordinal);

        var builder = new StringBuilder();
        foreach (var type in types)
        {
            builder.Append(DescribeTypeKind(type)).Append(' ').Append(QualifiedName(type))
                .Append(DescribeGenericParams(type.IsGenericTypeDefinition ? type.GetGenericArguments() : []))
                .Append(DescribeBaseTypesAndInterfaces(type))
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
        if (type.IsValueType) return "struct";
        if (typeof(System.Delegate).IsAssignableFrom(type)) return "delegate";
        if (type.IsAbstract && type.IsSealed) return "static class";
        if (type.IsSealed) return "sealed class";
        if (type.IsAbstract) return "abstract class";
        return "class";
    }

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

    private const string ApprovedSurface =
        "sealed class ArchLinterNet.CEL.CelEnvironment\n" +
        "  method ArchLinterNet.CEL.Compilation.CelCompilationResult<ArchLinterNet.CEL.Compilation.CelCompiledExpression> Compile(string source)\n" +
        "  method ArchLinterNet.CEL.Compilation.CelCompilationResult<ArchLinterNet.CEL.Compilation.CelCompiledPredicate> CompilePredicate(string source)\n" +
        "  method ArchLinterNet.CEL.Evaluation.CelEvaluationContextBuilder CreateEvaluationContextBuilder()\n" +
        "  method static ArchLinterNet.CEL.CelEnvironmentBuilder CreateBuilder(ArchLinterNet.CEL.Profile.CelProfile profile)\n" +
        "  property ArchLinterNet.CEL.Compilation.CelCompilationLimits CompilationLimits {get;}\n" +
        "  property ArchLinterNet.CEL.Evaluation.CelEvaluationLimits EvaluationLimits {get;}\n" +
        "  property ArchLinterNet.CEL.Profile.CelProfile Profile {get;}\n" +
        "  property ArchLinterNet.CEL.Schema.CelContextSchema Schema {get;}\n" +
        "  property System.Collections.Generic.IReadOnlyDictionary<string,ArchLinterNet.CEL.Schema.CelObjectSchema> ObjectSchemas {get;}\n" +
        "sealed class ArchLinterNet.CEL.CelEnvironmentBuilder\n" +
        "  method ArchLinterNet.CEL.CelEnvironment Build()\n" +
        "  method ArchLinterNet.CEL.CelEnvironmentBuilder WithCompilationLimits(ArchLinterNet.CEL.Compilation.CelCompilationLimits limits)\n" +
        "  method ArchLinterNet.CEL.CelEnvironmentBuilder WithContextSchema(ArchLinterNet.CEL.Schema.CelContextSchema schema)\n" +
        "  method ArchLinterNet.CEL.CelEnvironmentBuilder WithEvaluationLimits(ArchLinterNet.CEL.Evaluation.CelEvaluationLimits limits)\n" +
        "  method ArchLinterNet.CEL.CelEnvironmentBuilder WithObjectSchema(ArchLinterNet.CEL.Schema.CelObjectSchema objectSchema)\n" +
        "sealed class ArchLinterNet.CEL.Compilation.CelCompilationKey : System.IEquatable<ArchLinterNet.CEL.Compilation.CelCompilationKey>\n" +
        "  method bool Equals(ArchLinterNet.CEL.Compilation.CelCompilationKey? other)\n" +
        "  method bool Equals(object? obj)\n" +
        "  method int GetHashCode()\n" +
        "  method string ToString()\n" +
        "  property ArchLinterNet.CEL.Compilation.CelRequiredResultType RequiredResultType {get;}\n" +
        "  property ArchLinterNet.CEL.Profile.CelProfileId ProfileId {get;}\n" +
        "  property string CompilationLimitsIdentity {get;}\n" +
        "  property string EvaluationLimitsIdentity {get;}\n" +
        "  property string NormalizedSource {get;}\n" +
        "  property string SchemaIdentity {get;}\n" +
        "sealed class ArchLinterNet.CEL.Compilation.CelCompilationLimits\n" +
        "  ctor(int maxExpressionLength, int maxNestingDepth, int maxIdentifierCount, int maxTokenCount, int maxAstNodeCount, int maxLiteralSize)\n" +
        "  field static readonly ArchLinterNet.CEL.Compilation.CelCompilationLimits SafeDefaults\n" +
        "  property int MaxAstNodeCount {get;}\n" +
        "  property int MaxExpressionLength {get;}\n" +
        "  property int MaxIdentifierCount {get;}\n" +
        "  property int MaxLiteralSize {get;}\n" +
        "  property int MaxNestingDepth {get;}\n" +
        "  property int MaxTokenCount {get;}\n" +
        "sealed class ArchLinterNet.CEL.Compilation.CelCompilationResult<T where T : class>\n" +
        "  property ArchLinterNet.CEL.Compilation.CelCompilationKey CompilationKey {get;}\n" +
        "  property System.Collections.Generic.IReadOnlyList<ArchLinterNet.CEL.Diagnostics.CelDiagnostic> Diagnostics {get;}\n" +
        "  property T? Program {get;}\n" +
        "  property bool IsSuccess {get;}\n" +
        "sealed class ArchLinterNet.CEL.Compilation.CelCompiledExpression\n" +
        "  method ArchLinterNet.CEL.Evaluation.CelEvaluationResult Evaluate(ArchLinterNet.CEL.Evaluation.CelEvaluationContext context)\n" +
        "  method ArchLinterNet.CEL.Evaluation.CelEvaluationResult Evaluate(ArchLinterNet.CEL.Evaluation.CelEvaluationContext context, ArchLinterNet.CEL.Evaluation.CelEvaluationLimits limits)\n" +
        "  property ArchLinterNet.CEL.Compilation.CelCompilationKey CompilationKey {get;}\n" +
        "  property ArchLinterNet.CEL.Compilation.CelCompilationLimits CompilationLimits {get;}\n" +
        "  property ArchLinterNet.CEL.Evaluation.CelEvaluationLimits EvaluationLimits {get;}\n" +
        "  property ArchLinterNet.CEL.Profile.CelProfile Profile {get;}\n" +
        "  property ArchLinterNet.CEL.Schema.CelContextSchema Schema {get;}\n" +
        "sealed class ArchLinterNet.CEL.Compilation.CelCompiledPredicate\n" +
        "  method ArchLinterNet.CEL.Evaluation.CelEvaluationResult Evaluate(ArchLinterNet.CEL.Evaluation.CelEvaluationContext context)\n" +
        "  method ArchLinterNet.CEL.Evaluation.CelEvaluationResult Evaluate(ArchLinterNet.CEL.Evaluation.CelEvaluationContext context, ArchLinterNet.CEL.Evaluation.CelEvaluationLimits limits)\n" +
        "  property ArchLinterNet.CEL.Compilation.CelCompilationKey CompilationKey {get;}\n" +
        "  property ArchLinterNet.CEL.Compilation.CelCompilationLimits CompilationLimits {get;}\n" +
        "  property ArchLinterNet.CEL.Evaluation.CelEvaluationLimits EvaluationLimits {get;}\n" +
        "  property ArchLinterNet.CEL.Profile.CelProfile Profile {get;}\n" +
        "  property ArchLinterNet.CEL.Schema.CelContextSchema Schema {get;}\n" +
        "enum ArchLinterNet.CEL.Compilation.CelRequiredResultType\n" +
        "  enum-member General = 1\n" +
        "  enum-member Predicate = 0\n" +
        "sealed class ArchLinterNet.CEL.Diagnostics.CelDiagnostic\n" +
        "  method string ToString()\n" +
        "  property ArchLinterNet.CEL.Diagnostics.CelDiagnosticCode Code {get;}\n" +
        "  property ArchLinterNet.CEL.Diagnostics.CelDiagnosticSeverity Severity {get;}\n" +
        "  property ArchLinterNet.CEL.Diagnostics.CelSourceSpan? Span {get;}\n" +
        "  property System.Collections.Generic.IReadOnlyDictionary<string,string> Parameters {get;}\n" +
        "  property string Category {get;}\n" +
        "  property string Message {get;}\n" +
        "enum ArchLinterNet.CEL.Diagnostics.CelDiagnosticCode\n" +
        "  enum-member BindingError = 2\n" +
        "  enum-member BudgetExceeded = 5\n" +
        "  enum-member EvaluationFailure = 6\n" +
        "  enum-member NotYetImplemented = 7\n" +
        "  enum-member SchemaMismatch = 4\n" +
        "  enum-member SyntaxError = 0\n" +
        "  enum-member TypeMismatch = 3\n" +
        "  enum-member UnsupportedFeature = 1\n" +
        "enum ArchLinterNet.CEL.Diagnostics.CelDiagnosticSeverity\n" +
        "  enum-member Error = 0\n" +
        "  enum-member Info = 2\n" +
        "  enum-member Warning = 1\n" +
        "struct ArchLinterNet.CEL.Diagnostics.CelSourceSpan : System.IEquatable<ArchLinterNet.CEL.Diagnostics.CelSourceSpan>\n" +
        "  ctor(int start, int end)\n" +
        "  method bool Equals(ArchLinterNet.CEL.Diagnostics.CelSourceSpan other)\n" +
        "  method bool Equals(object? obj)\n" +
        "  method int GetHashCode()\n" +
        "  method string ToString()\n" +
        "  operator !=(ArchLinterNet.CEL.Diagnostics.CelSourceSpan left, ArchLinterNet.CEL.Diagnostics.CelSourceSpan right)\n" +
        "  operator ==(ArchLinterNet.CEL.Diagnostics.CelSourceSpan left, ArchLinterNet.CEL.Diagnostics.CelSourceSpan right)\n" +
        "  property int End {get;}\n" +
        "  property int Start {get;}\n" +
        "sealed class ArchLinterNet.CEL.Evaluation.CelEvaluationContext\n" +
        "  property ArchLinterNet.CEL.Schema.CelContextSchema Schema {get;}\n" +
        "  property System.Collections.Generic.IReadOnlyList<(ArchLinterNet.CEL.Schema.CelVariable Variable, ArchLinterNet.CEL.Values.CelValue Value)> Assignments {get;}\n" +
        "sealed class ArchLinterNet.CEL.Evaluation.CelEvaluationContextBuilder\n" +
        "  method ArchLinterNet.CEL.Evaluation.CelEvaluationContext Build()\n" +
        "  method ArchLinterNet.CEL.Evaluation.CelEvaluationContextBuilder Set(ArchLinterNet.CEL.Schema.CelVariable variable, ArchLinterNet.CEL.Values.CelValue value)\n" +
        "  method ArchLinterNet.CEL.Evaluation.CelEvaluationContextBuilder Set(string name, ArchLinterNet.CEL.Values.CelValue value)\n" +
        "sealed class ArchLinterNet.CEL.Evaluation.CelEvaluationLimits\n" +
        "  ctor(int maxIterations, long maxCostUnits)\n" +
        "  field static readonly ArchLinterNet.CEL.Evaluation.CelEvaluationLimits SafeDefaults\n" +
        "  property int MaxIterations {get;}\n" +
        "  property long MaxCostUnits {get;}\n" +
        "sealed class ArchLinterNet.CEL.Evaluation.CelEvaluationResult\n" +
        "  method bool AsBool()\n" +
        "  property ArchLinterNet.CEL.Values.CelValue? Value {get;}\n" +
        "  property System.Collections.Generic.IReadOnlyList<ArchLinterNet.CEL.Diagnostics.CelDiagnostic> Diagnostics {get;}\n" +
        "  property bool IsSuccess {get;}\n" +
        "sealed class ArchLinterNet.CEL.Profile.CelProfile\n" +
        "  field static readonly ArchLinterNet.CEL.Profile.CelProfile V1\n" +
        "  method string ToString()\n" +
        "  property ArchLinterNet.CEL.Profile.CelProfileId Id {get;}\n" +
        "struct ArchLinterNet.CEL.Profile.CelProfileId : System.IEquatable<ArchLinterNet.CEL.Profile.CelProfileId>\n" +
        "  implicit operator ArchLinterNet.CEL.Profile.CelProfileId(string value)\n" +
        "  method bool Equals(ArchLinterNet.CEL.Profile.CelProfileId other)\n" +
        "  method bool Equals(object? obj)\n" +
        "  method int GetHashCode()\n" +
        "  method string ToString()\n" +
        "  operator !=(ArchLinterNet.CEL.Profile.CelProfileId left, ArchLinterNet.CEL.Profile.CelProfileId right)\n" +
        "  operator ==(ArchLinterNet.CEL.Profile.CelProfileId left, ArchLinterNet.CEL.Profile.CelProfileId right)\n" +
        "  property string Value {get;}\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelContextSchema\n" +
        "  method ArchLinterNet.CEL.Evaluation.CelEvaluationContextBuilder CreateEvaluationContextBuilder()\n" +
        "  method static ArchLinterNet.CEL.Schema.CelContextSchemaBuilder CreateBuilder(string schemaId)\n" +
        "  method string ToString()\n" +
        "  property System.Collections.Generic.IReadOnlyList<ArchLinterNet.CEL.Schema.CelVariable> Variables {get;}\n" +
        "  property string Identity {get;}\n" +
        "  property string SchemaId {get;}\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelContextSchemaBuilder\n" +
        "  method ArchLinterNet.CEL.Schema.CelContextSchema Build()\n" +
        "  method ArchLinterNet.CEL.Schema.CelVariable AddVariable(string name, ArchLinterNet.CEL.Schema.CelType type)\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelObjectMember\n" +
        "  property ArchLinterNet.CEL.Schema.CelType Type {get;}\n" +
        "  property string Name {get;}\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelObjectSchema\n" +
        "  method static ArchLinterNet.CEL.Schema.CelObjectSchemaBuilder CreateBuilder(string objectTypeId)\n" +
        "  property System.Collections.Generic.IReadOnlyList<ArchLinterNet.CEL.Schema.CelObjectMember> Members {get;}\n" +
        "  property string ObjectTypeId {get;}\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelObjectSchemaBuilder\n" +
        "  method ArchLinterNet.CEL.Schema.CelObjectMember AddMember(string name, ArchLinterNet.CEL.Schema.CelType type)\n" +
        "  method ArchLinterNet.CEL.Schema.CelObjectSchema Build()\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelType\n" +
        "  method static ArchLinterNet.CEL.Schema.CelType ListOf(ArchLinterNet.CEL.Schema.CelType elementType)\n" +
        "  method static ArchLinterNet.CEL.Schema.CelType MapOf(ArchLinterNet.CEL.Schema.CelType valueType)\n" +
        "  method static ArchLinterNet.CEL.Schema.CelType ObjectOf(string schemaId)\n" +
        "  method string ToString()\n" +
        "  property ArchLinterNet.CEL.Schema.CelType? ElementType {get;}\n" +
        "  property ArchLinterNet.CEL.Schema.CelType? ValueType {get;}\n" +
        "  property ArchLinterNet.CEL.Schema.CelTypeKind Kind {get;}\n" +
        "  property static ArchLinterNet.CEL.Schema.CelType Bool {get;}\n" +
        "  property static ArchLinterNet.CEL.Schema.CelType Float {get;}\n" +
        "  property static ArchLinterNet.CEL.Schema.CelType Int {get;}\n" +
        "  property static ArchLinterNet.CEL.Schema.CelType String {get;}\n" +
        "  property string? SchemaId {get;}\n" +
        "enum ArchLinterNet.CEL.Schema.CelTypeKind\n" +
        "  enum-member Bool = 0\n" +
        "  enum-member Float = 3\n" +
        "  enum-member Int = 2\n" +
        "  enum-member List = 4\n" +
        "  enum-member Map = 5\n" +
        "  enum-member Object = 6\n" +
        "  enum-member String = 1\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelVariable\n" +
        "  method string ToString()\n" +
        "  property ArchLinterNet.CEL.Schema.CelType Type {get;}\n" +
        "  property string Name {get;}\n" +
        "sealed class ArchLinterNet.CEL.Values.CelObjectValue\n" +
        "  ctor(string objectTypeId, System.Collections.Generic.IReadOnlyDictionary<string,ArchLinterNet.CEL.Values.CelValue> members)\n" +
        "  method string ToString()\n" +
        "  property System.Collections.Generic.IReadOnlyDictionary<string,ArchLinterNet.CEL.Values.CelValue> Members {get;}\n" +
        "  property string ObjectTypeId {get;}\n" +
        "sealed class ArchLinterNet.CEL.Values.CelValue\n" +
        "  method ArchLinterNet.CEL.Values.CelObjectValue AsObject()\n" +
        "  method System.Collections.Generic.IReadOnlyDictionary<string,ArchLinterNet.CEL.Values.CelValue> AsMap()\n" +
        "  method System.Collections.Generic.IReadOnlyList<ArchLinterNet.CEL.Values.CelValue> AsList()\n" +
        "  method bool AsBool()\n" +
        "  method double AsFloat()\n" +
        "  method long AsInt()\n" +
        "  method static ArchLinterNet.CEL.Values.CelValue Bool(bool value)\n" +
        "  method static ArchLinterNet.CEL.Values.CelValue Float(double value)\n" +
        "  method static ArchLinterNet.CEL.Values.CelValue Int(long value)\n" +
        "  method static ArchLinterNet.CEL.Values.CelValue List(System.Collections.Generic.IReadOnlyList<ArchLinterNet.CEL.Values.CelValue> value)\n" +
        "  method static ArchLinterNet.CEL.Values.CelValue Map(System.Collections.Generic.IReadOnlyDictionary<string,ArchLinterNet.CEL.Values.CelValue> value)\n" +
        "  method static ArchLinterNet.CEL.Values.CelValue Object(ArchLinterNet.CEL.Values.CelObjectValue value)\n" +
        "  method static ArchLinterNet.CEL.Values.CelValue String(string value)\n" +
        "  method string AsString()\n" +
        "  method string ToString()\n" +
        "  property ArchLinterNet.CEL.Values.CelValueKind Kind {get;}\n" +
        "enum ArchLinterNet.CEL.Values.CelValueKind\n" +
        "  enum-member Bool = 0\n" +
        "  enum-member Float = 3\n" +
        "  enum-member Int = 2\n" +
        "  enum-member List = 4\n" +
        "  enum-member Map = 5\n" +
        "  enum-member Object = 6\n" +
        "  enum-member String = 1\n";
}
