using System.Linq;
using System.Reflection;
using System.Text;
using ArchLinterNet.CEL;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Approval test (#329) that enumerates the entire public API surface of
/// <c>ArchLinterNet.CEL</c> — every public and public-nested type, its constructors, fields
/// (including static/readonly/const modifiers), properties, operators/conversions, methods
/// (including generic constraints), and enum members, all with nullable-reference annotations —
/// and compares it against an approved baseline. Any accidental addition, removal, or signature
/// change to the public surface fails this test with a readable diff, instead of silently
/// shipping an API change. Update <see cref="ApprovedSurface"/> deliberately when the public API
/// intentionally changes.
/// </summary>
[TestFixture]
public sealed class CelPublicApiSurfaceApprovalTests
{
    private static readonly NullabilityInfoContext _nullabilityCtx = new();

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
            .OrderBy(t => t.FullName, System.StringComparer.Ordinal);

        var builder = new StringBuilder();
        foreach (var type in types)
        {
            builder.Append(DescribeTypeKind(type)).Append(' ').Append(type.FullName)
                .Append(DescribeGenericParams(type.IsGenericTypeDefinition ? type.GetGenericArguments() : []))
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

    private static System.Collections.Generic.IEnumerable<string> DescribeMembers(System.Type type)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                                    BindingFlags.DeclaredOnly;

        if (type.IsEnum)
        {
            foreach (var name in System.Enum.GetNames(type).OrderBy(n => n, System.StringComparer.Ordinal))
                yield return $"enum-member {name}";
            yield break;
        }

        var descriptions = new System.Collections.Generic.List<string>();

        foreach (var ctor in type.GetConstructors(Flags))
            descriptions.Add($"ctor({Params(ctor.GetParameters())})");

        foreach (var field in type.GetFields(Flags))
            descriptions.Add($"field {FieldModifiers(field)}{TypeName(field.FieldType)}{FieldNullability(field)} {field.Name}");

        foreach (var prop in type.GetProperties(Flags))
        {
            var accessors = (prop.CanRead ? "get" : "") + (prop.CanWrite ? "set" : "");
            descriptions.Add($"property {TypeName(prop.PropertyType)}{PropertyNullability(prop)} {prop.Name} {{{accessors}}}");
        }

        var accessorMethods = new System.Collections.Generic.HashSet<MethodInfo>();
        foreach (var prop in type.GetProperties(Flags))
        {
            if (prop.GetMethod is not null) accessorMethods.Add(prop.GetMethod);
            if (prop.SetMethod is not null) accessorMethods.Add(prop.SetMethod);
        }

        foreach (var evt in type.GetEvents(Flags))
        {
            if (evt.AddMethod is not null) accessorMethods.Add(evt.AddMethod);
            if (evt.RemoveMethod is not null) accessorMethods.Add(evt.RemoveMethod);
        }

        foreach (var method in type.GetMethods(Flags).Where(m => !accessorMethods.Contains(m)))
        {
            if (method.IsSpecialName && method.Name.StartsWith("op_", System.StringComparison.Ordinal))
            {
                descriptions.Add(DescribeOperator(method));
                continue;
            }

            var generic = method.IsGenericMethodDefinition
                ? DescribeGenericParams(method.GetGenericArguments())
                : "";
            var returnNullability = MethodReturnNullability(method);
            descriptions.Add($"method {TypeName(method.ReturnType)}{returnNullability} {method.Name}{generic}({Params(method.GetParameters())})");
        }

        foreach (var op in descriptions.OrderBy(d => d, System.StringComparer.Ordinal))
            yield return op;
    }

    private static string DescribeOperator(MethodInfo method)
    {
        if (method.Name is "op_Implicit" or "op_Explicit")
        {
            var kind = method.Name == "op_Implicit" ? "implicit" : "explicit";
            return $"{kind} operator {TypeName(method.ReturnType)}({Params(method.GetParameters())})";
        }

        var symbol = OperatorSymbol(method.Name);
        return $"operator {symbol}({Params(method.GetParameters())})";
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

    private static string FieldModifiers(FieldInfo field)
    {
        if (field.IsLiteral) return "const ";
        var modifiers = new System.Collections.Generic.List<string>();
        if (field.IsStatic) modifiers.Add("static");
        if (field.IsInitOnly) modifiers.Add("readonly");
        return modifiers.Count == 0 ? "" : string.Join(" ", modifiers) + " ";
    }

    private static string FieldNullability(FieldInfo field)
    {
        if (field.FieldType.IsValueType) return "";
        var info = _nullabilityCtx.Create(field);
        return info.ReadState == NullabilityState.Nullable ? "?" : "";
    }

    private static string PropertyNullability(PropertyInfo prop)
    {
        if (prop.PropertyType.IsValueType) return "";
        var info = _nullabilityCtx.Create(prop);
        return info.ReadState == NullabilityState.Nullable ? "?" : "";
    }

    private static string MethodReturnNullability(MethodInfo method)
    {
        if (method.ReturnType.IsValueType || method.ReturnType == typeof(void)) return "";
        var info = _nullabilityCtx.Create(method.ReturnParameter);
        return info.ReadState == NullabilityState.Nullable ? "?" : "";
    }

    private static string ParamNullability(ParameterInfo parameter)
    {
        if (parameter.ParameterType.IsValueType) return "";
        var info = _nullabilityCtx.Create(parameter);
        return info.ReadState == NullabilityState.Nullable ? "?" : "";
    }

    private static string Params(ParameterInfo[] parameters) =>
        string.Join(", ", parameters.Select(p => $"{TypeName(p.ParameterType)}{ParamNullability(p)} {p.Name}"));

    private static string DescribeGenericParams(System.Type[] typeArgs)
    {
        if (typeArgs.Length == 0) return "";
        return $"<{string.Join(",", typeArgs.Select(DescribeGenericParam))}>";
    }

    private static string DescribeGenericParam(System.Type t)
    {
        var constraints = new System.Collections.Generic.List<string>();
        var attrs = t.GenericParameterAttributes;
        if (attrs.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint)) constraints.Add("class");
        if (attrs.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint)) constraints.Add("struct");
        foreach (var c in t.GetGenericParameterConstraints())
            if (c != typeof(System.ValueType)) constraints.Add(TypeName(c));
        if (attrs.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) &&
            !attrs.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            constraints.Add("new()");
        return constraints.Count == 0 ? t.Name : $"{t.Name} where {t.Name} : {string.Join(",", constraints)}";
    }

    private static string TypeName(System.Type type)
    {
        if (!type.IsGenericType) return type.Name;
        var name = type.Name[..type.Name.IndexOf('`')];
        var args = string.Join(",", type.GetGenericArguments().Select(TypeName));
        return $"{name}<{args}>";
    }

    private const string ApprovedSurface =
        "sealed class ArchLinterNet.CEL.CelEnvironment\n" +
        "  method CelCompilationResult<CelCompiledExpression> Compile(String source)\n" +
        "  method CelCompilationResult<CelCompiledPredicate> CompilePredicate(String source)\n" +
        "  method CelEnvironmentBuilder CreateBuilder(CelProfile profile)\n" +
        "  method CelEvaluationContextBuilder CreateEvaluationContextBuilder()\n" +
        "  property CelCompilationLimits CompilationLimits {get}\n" +
        "  property CelContextSchema Schema {get}\n" +
        "  property CelEvaluationLimits EvaluationLimits {get}\n" +
        "  property CelProfile Profile {get}\n" +
        "  property IReadOnlyDictionary<String,CelObjectSchema> ObjectSchemas {get}\n" +
        "sealed class ArchLinterNet.CEL.CelEnvironmentBuilder\n" +
        "  method CelEnvironment Build()\n" +
        "  method CelEnvironmentBuilder WithCompilationLimits(CelCompilationLimits limits)\n" +
        "  method CelEnvironmentBuilder WithContextSchema(CelContextSchema schema)\n" +
        "  method CelEnvironmentBuilder WithEvaluationLimits(CelEvaluationLimits limits)\n" +
        "  method CelEnvironmentBuilder WithObjectSchema(CelObjectSchema objectSchema)\n" +
        "sealed class ArchLinterNet.CEL.Compilation.CelCompilationKey\n" +
        "  method Boolean Equals(CelCompilationKey? other)\n" +
        "  method Boolean Equals(Object? obj)\n" +
        "  method Int32 GetHashCode()\n" +
        "  method String ToString()\n" +
        "  property CelProfileId ProfileId {get}\n" +
        "  property CelRequiredResultType RequiredResultType {get}\n" +
        "  property String CompilationLimitsIdentity {get}\n" +
        "  property String EvaluationLimitsIdentity {get}\n" +
        "  property String NormalizedSource {get}\n" +
        "  property String SchemaIdentity {get}\n" +
        "sealed class ArchLinterNet.CEL.Compilation.CelCompilationLimits\n" +
        "  ctor(Int32 maxExpressionLength, Int32 maxNestingDepth, Int32 maxIdentifierCount, Int32 maxTokenCount, Int32 maxAstNodeCount, Int32 maxLiteralSize)\n" +
        "  field static readonly CelCompilationLimits SafeDefaults\n" +
        "  property Int32 MaxAstNodeCount {get}\n" +
        "  property Int32 MaxExpressionLength {get}\n" +
        "  property Int32 MaxIdentifierCount {get}\n" +
        "  property Int32 MaxLiteralSize {get}\n" +
        "  property Int32 MaxNestingDepth {get}\n" +
        "  property Int32 MaxTokenCount {get}\n" +
        "sealed class ArchLinterNet.CEL.Compilation.CelCompilationResult`1<T where T : class>\n" +
        "  property Boolean IsSuccess {get}\n" +
        "  property CelCompilationKey CompilationKey {get}\n" +
        "  property IReadOnlyList<CelDiagnostic> Diagnostics {get}\n" +
        "  property T? Program {get}\n" +
        "sealed class ArchLinterNet.CEL.Compilation.CelCompiledExpression\n" +
        "  method CelEvaluationResult Evaluate(CelEvaluationContext context)\n" +
        "  method CelEvaluationResult Evaluate(CelEvaluationContext context, CelEvaluationLimits limits)\n" +
        "  property CelCompilationKey CompilationKey {get}\n" +
        "  property CelCompilationLimits CompilationLimits {get}\n" +
        "  property CelContextSchema Schema {get}\n" +
        "  property CelEvaluationLimits EvaluationLimits {get}\n" +
        "  property CelProfile Profile {get}\n" +
        "sealed class ArchLinterNet.CEL.Compilation.CelCompiledPredicate\n" +
        "  method CelEvaluationResult Evaluate(CelEvaluationContext context)\n" +
        "  method CelEvaluationResult Evaluate(CelEvaluationContext context, CelEvaluationLimits limits)\n" +
        "  property CelCompilationKey CompilationKey {get}\n" +
        "  property CelCompilationLimits CompilationLimits {get}\n" +
        "  property CelContextSchema Schema {get}\n" +
        "  property CelEvaluationLimits EvaluationLimits {get}\n" +
        "  property CelProfile Profile {get}\n" +
        "enum ArchLinterNet.CEL.Compilation.CelRequiredResultType\n" +
        "  enum-member General\n" +
        "  enum-member Predicate\n" +
        "sealed class ArchLinterNet.CEL.Diagnostics.CelDiagnostic\n" +
        "  method String ToString()\n" +
        "  property CelDiagnosticCode Code {get}\n" +
        "  property CelDiagnosticSeverity Severity {get}\n" +
        "  property IReadOnlyDictionary<String,String> Parameters {get}\n" +
        "  property Nullable<CelSourceSpan> Span {get}\n" +
        "  property String Category {get}\n" +
        "  property String Message {get}\n" +
        "enum ArchLinterNet.CEL.Diagnostics.CelDiagnosticCode\n" +
        "  enum-member BindingError\n" +
        "  enum-member BudgetExceeded\n" +
        "  enum-member EvaluationFailure\n" +
        "  enum-member NotYetImplemented\n" +
        "  enum-member SchemaMismatch\n" +
        "  enum-member SyntaxError\n" +
        "  enum-member TypeMismatch\n" +
        "  enum-member UnsupportedFeature\n" +
        "enum ArchLinterNet.CEL.Diagnostics.CelDiagnosticSeverity\n" +
        "  enum-member Error\n" +
        "  enum-member Info\n" +
        "  enum-member Warning\n" +
        "struct ArchLinterNet.CEL.Diagnostics.CelSourceSpan\n" +
        "  ctor(Int32 start, Int32 end)\n" +
        "  method Boolean Equals(CelSourceSpan other)\n" +
        "  method Boolean Equals(Object? obj)\n" +
        "  method Int32 GetHashCode()\n" +
        "  method String ToString()\n" +
        "  operator !=(CelSourceSpan left, CelSourceSpan right)\n" +
        "  operator ==(CelSourceSpan left, CelSourceSpan right)\n" +
        "  property Int32 End {get}\n" +
        "  property Int32 Start {get}\n" +
        "sealed class ArchLinterNet.CEL.Evaluation.CelEvaluationContext\n" +
        "  property CelContextSchema Schema {get}\n" +
        "  property IReadOnlyList<ValueTuple<CelVariable,CelValue>> Assignments {get}\n" +
        "sealed class ArchLinterNet.CEL.Evaluation.CelEvaluationContextBuilder\n" +
        "  method CelEvaluationContext Build()\n" +
        "  method CelEvaluationContextBuilder Set(CelVariable variable, CelValue value)\n" +
        "  method CelEvaluationContextBuilder Set(String name, CelValue value)\n" +
        "sealed class ArchLinterNet.CEL.Evaluation.CelEvaluationLimits\n" +
        "  ctor(Int32 maxIterations, Int64 maxCostUnits)\n" +
        "  field static readonly CelEvaluationLimits SafeDefaults\n" +
        "  property Int32 MaxIterations {get}\n" +
        "  property Int64 MaxCostUnits {get}\n" +
        "sealed class ArchLinterNet.CEL.Evaluation.CelEvaluationResult\n" +
        "  method Boolean AsBool()\n" +
        "  property Boolean IsSuccess {get}\n" +
        "  property CelValue? Value {get}\n" +
        "  property IReadOnlyList<CelDiagnostic> Diagnostics {get}\n" +
        "sealed class ArchLinterNet.CEL.Profile.CelProfile\n" +
        "  field static readonly CelProfile V1\n" +
        "  method String ToString()\n" +
        "  property CelProfileId Id {get}\n" +
        "struct ArchLinterNet.CEL.Profile.CelProfileId\n" +
        "  implicit operator CelProfileId(String value)\n" +
        "  method Boolean Equals(CelProfileId other)\n" +
        "  method Boolean Equals(Object? obj)\n" +
        "  method Int32 GetHashCode()\n" +
        "  method String ToString()\n" +
        "  operator !=(CelProfileId left, CelProfileId right)\n" +
        "  operator ==(CelProfileId left, CelProfileId right)\n" +
        "  property String Value {get}\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelContextSchema\n" +
        "  method CelContextSchemaBuilder CreateBuilder(String schemaId)\n" +
        "  method CelEvaluationContextBuilder CreateEvaluationContextBuilder()\n" +
        "  method String ToString()\n" +
        "  property IReadOnlyList<CelVariable> Variables {get}\n" +
        "  property String Identity {get}\n" +
        "  property String SchemaId {get}\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelContextSchemaBuilder\n" +
        "  method CelContextSchema Build()\n" +
        "  method CelVariable AddVariable(String name, CelType type)\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelObjectMember\n" +
        "  property CelType Type {get}\n" +
        "  property String Name {get}\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelObjectSchema\n" +
        "  method CelObjectSchemaBuilder CreateBuilder(String objectTypeId)\n" +
        "  property IReadOnlyList<CelObjectMember> Members {get}\n" +
        "  property String ObjectTypeId {get}\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelObjectSchemaBuilder\n" +
        "  method CelObjectMember AddMember(String name, CelType type)\n" +
        "  method CelObjectSchema Build()\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelType\n" +
        "  method CelType ListOf(CelType elementType)\n" +
        "  method CelType MapOf(CelType valueType)\n" +
        "  method CelType ObjectOf(String schemaId)\n" +
        "  method String ToString()\n" +
        "  property CelType Bool {get}\n" +
        "  property CelType Float {get}\n" +
        "  property CelType Int {get}\n" +
        "  property CelType String {get}\n" +
        "  property CelType? ElementType {get}\n" +
        "  property CelType? ValueType {get}\n" +
        "  property CelTypeKind Kind {get}\n" +
        "  property String? SchemaId {get}\n" +
        "enum ArchLinterNet.CEL.Schema.CelTypeKind\n" +
        "  enum-member Bool\n" +
        "  enum-member Float\n" +
        "  enum-member Int\n" +
        "  enum-member List\n" +
        "  enum-member Map\n" +
        "  enum-member Object\n" +
        "  enum-member String\n" +
        "sealed class ArchLinterNet.CEL.Schema.CelVariable\n" +
        "  method String ToString()\n" +
        "  property CelType Type {get}\n" +
        "  property String Name {get}\n" +
        "sealed class ArchLinterNet.CEL.Values.CelObjectValue\n" +
        "  ctor(String objectTypeId, IReadOnlyDictionary<String,CelValue> members)\n" +
        "  method String ToString()\n" +
        "  property IReadOnlyDictionary<String,CelValue> Members {get}\n" +
        "  property String ObjectTypeId {get}\n" +
        "sealed class ArchLinterNet.CEL.Values.CelValue\n" +
        "  method Boolean AsBool()\n" +
        "  method CelObjectValue AsObject()\n" +
        "  method CelValue Bool(Boolean value)\n" +
        "  method CelValue Float(Double value)\n" +
        "  method CelValue Int(Int64 value)\n" +
        "  method CelValue List(IReadOnlyList<CelValue> value)\n" +
        "  method CelValue Map(IReadOnlyDictionary<String,CelValue> value)\n" +
        "  method CelValue Object(CelObjectValue value)\n" +
        "  method CelValue String(String value)\n" +
        "  method Double AsFloat()\n" +
        "  method IReadOnlyDictionary<String,CelValue> AsMap()\n" +
        "  method IReadOnlyList<CelValue> AsList()\n" +
        "  method Int64 AsInt()\n" +
        "  method String AsString()\n" +
        "  method String ToString()\n" +
        "  property CelValueKind Kind {get}\n" +
        "enum ArchLinterNet.CEL.Values.CelValueKind\n" +
        "  enum-member Bool\n" +
        "  enum-member Float\n" +
        "  enum-member Int\n" +
        "  enum-member List\n" +
        "  enum-member Map\n" +
        "  enum-member Object\n" +
        "  enum-member String\n";
}
