namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// Stable internal identifier for one Profile v1 built-in function overload's execution behavior.
/// </summary>
/// <remarks>
/// <see cref="CelFunctionOverload.OperationId"/> carries one of these per catalog row so a future
/// evaluator dispatches execution by this identifier — never by re-parsing the function name string
/// or receiver kind a second time. See <c>CelBuiltinFunctionInvoker</c> for the corresponding pure
/// implementation of each member.
/// </remarks>
internal enum CelFunctionOperationId
{
    /// <summary><c>String.startsWith(String) -&gt; Bool</c>.</summary>
    StartsWith,

    /// <summary><c>String.endsWith(String) -&gt; Bool</c>.</summary>
    EndsWith,

    /// <summary><c>String.contains(String) -&gt; Bool</c>.</summary>
    Contains,

    /// <summary><c>String.size() -&gt; Int</c> (Unicode code-point count).</summary>
    SizeString,

    /// <summary><c>List.size() -&gt; Int</c> (element count).</summary>
    SizeList,

    /// <summary><c>Map.size() -&gt; Int</c> (entry count).</summary>
    SizeMap,

    /// <summary><c>Map.containsKey(String) -&gt; Bool</c>.</summary>
    ContainsKey,
}
