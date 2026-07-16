using ArchLinterNet.CEL.Schema;

namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// The complete, closed built-in function overload catalog for ArchLinter CEL Profile v1. The
/// binder resolves every <c>CelCallSyntax</c> against exactly this table and nothing else — no
/// user-registered functions, no reflection-based dispatch.
/// </summary>
internal static class CelFunctionCatalog
{
    private static readonly IReadOnlyList<CelFunctionOverload> _overloads =
    [
        new CelFunctionOverload("startsWith", CelTypeKind.String, [CelTypeKind.String], CelType.Bool),
        new CelFunctionOverload("endsWith", CelTypeKind.String, [CelTypeKind.String], CelType.Bool),
        new CelFunctionOverload("contains", CelTypeKind.String, [CelTypeKind.String], CelType.Bool),
        new CelFunctionOverload("size", CelTypeKind.String, [], CelType.Int),
        new CelFunctionOverload("size", CelTypeKind.List, [], CelType.Int),
        new CelFunctionOverload("size", CelTypeKind.Map, [], CelType.Int),
        new CelFunctionOverload("containsKey", CelTypeKind.Map, [CelTypeKind.String], CelType.Bool),
    ];

    /// <summary>
    /// Returns <c>true</c> when at least one overload exists for <paramref name="functionName"/>,
    /// regardless of receiver or argument shape. A <c>false</c> result means the name is entirely
    /// unknown to Profile v1's catalog.
    /// </summary>
    public static bool HasAnyOverload(string functionName) =>
        _overloads.Any(o => string.Equals(o.FunctionName, functionName, StringComparison.Ordinal));

    /// <summary>Returns every catalog overload declared for <paramref name="functionName"/>.</summary>
    public static IEnumerable<CelFunctionOverload> OverloadsFor(string functionName) =>
        _overloads.Where(o => string.Equals(o.FunctionName, functionName, StringComparison.Ordinal));
}
