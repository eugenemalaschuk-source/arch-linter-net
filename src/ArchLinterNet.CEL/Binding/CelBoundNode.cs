using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Parsing;
using ArchLinterNet.CEL.Schema;

namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// Base type for an immutable internal bound expression node produced by <see cref="CelBinder"/>.
/// Mirrors <see cref="CelSyntaxNode"/> 1:1, with each node additionally carrying its resolved
/// <see cref="CelType"/>.
/// </summary>
/// <remarks>
/// Internal only — never exposed publicly, held only by <c>CelCompiledPredicate</c>/
/// <c>CelCompiledExpression</c>. See the "Tooling and AST" extension-direction row in
/// <c>docs/internal/cel-engine-architecture.md</c>.
/// </remarks>
internal abstract class CelBoundNode
{
    /// <summary>Gets the source span of the syntax node this bound node was produced from.</summary>
    public CelSourceSpan Span { get; }

    /// <summary>Gets the statically resolved CEL type of this expression.</summary>
    public CelType ResolvedType { get; }

    protected CelBoundNode(CelSourceSpan span, CelType resolvedType)
    {
        Span = span;
        ResolvedType = resolvedType;
    }
}

/// <summary>A bound <c>bool</c> literal.</summary>
internal sealed class CelBoundBoolLiteral : CelBoundNode
{
    public bool Value { get; }

    public CelBoundBoolLiteral(CelSourceSpan span, bool value) : base(span, CelType.Bool) => Value = value;
}

/// <summary>A bound signed 64-bit integer literal.</summary>
internal sealed class CelBoundIntLiteral : CelBoundNode
{
    public long Value { get; }

    public CelBoundIntLiteral(CelSourceSpan span, long value) : base(span, CelType.Int) => Value = value;
}

/// <summary>A bound IEEE 754 double-precision floating-point literal.</summary>
internal sealed class CelBoundFloatLiteral : CelBoundNode
{
    public double Value { get; }

    public CelBoundFloatLiteral(CelSourceSpan span, double value) : base(span, CelType.Float) => Value = value;
}

/// <summary>A bound string literal.</summary>
internal sealed class CelBoundStringLiteral : CelBoundNode
{
    public string Value { get; }

    public CelBoundStringLiteral(CelSourceSpan span, string value) : base(span, CelType.String) => Value = value;
}

/// <summary>A bound reference to a schema-declared variable.</summary>
internal sealed class CelBoundIdentifier : CelBoundNode
{
    public CelVariable Variable { get; }

    public CelBoundIdentifier(CelSourceSpan span, CelVariable variable)
        : base(span, variable.Type) => Variable = variable;
}

/// <summary>A bound prefix unary expression, e.g. <c>!x</c>.</summary>
internal sealed class CelBoundUnary : CelBoundNode
{
    public CelUnaryOperator Operator { get; }

    public CelBoundNode Operand { get; }

    public CelBoundUnary(CelSourceSpan span, CelType resolvedType, CelUnaryOperator @operator, CelBoundNode operand)
        : base(span, resolvedType)
    {
        Operator = @operator;
        Operand = operand;
    }
}

/// <summary>A bound binary expression, e.g. <c>a &amp;&amp; b</c> or <c>a == b</c>.</summary>
internal sealed class CelBoundBinary : CelBoundNode
{
    public CelBinaryOperator Operator { get; }

    public CelBoundNode Left { get; }

    public CelBoundNode Right { get; }

    public CelBoundBinary(CelSourceSpan span, CelType resolvedType, CelBinaryOperator @operator, CelBoundNode left, CelBoundNode right)
        : base(span, resolvedType)
    {
        Operator = @operator;
        Left = left;
        Right = right;
    }
}

/// <summary>A bound dotted member access, e.g. <c>x.role</c>.</summary>
internal sealed class CelBoundMemberAccess : CelBoundNode
{
    public CelBoundNode Receiver { get; }

    public CelObjectMember Member { get; }

    public CelBoundMemberAccess(CelSourceSpan span, CelBoundNode receiver, CelObjectMember member)
        : base(span, member.Type)
    {
        Receiver = receiver;
        Member = member;
    }
}

/// <summary>A bound bracketed index expression, e.g. <c>x[0]</c> or <c>m["key"]</c>.</summary>
internal sealed class CelBoundIndex : CelBoundNode
{
    public CelBoundNode Receiver { get; }

    public CelBoundNode Index { get; }

    public CelBoundIndex(CelSourceSpan span, CelType resolvedType, CelBoundNode receiver, CelBoundNode index)
        : base(span, resolvedType)
    {
        Receiver = receiver;
        Index = index;
    }
}

/// <summary>
/// A bound function call resolved against the closed Profile v1 built-in function catalog.
/// </summary>
internal sealed class CelBoundCall : CelBoundNode
{
    public CelBoundNode? Receiver { get; }

    public string FunctionName { get; }

    public IReadOnlyList<CelBoundNode> Arguments { get; }

    public CelFunctionOverload Overload { get; }

    public CelBoundCall(
        CelSourceSpan span,
        CelBoundNode? receiver,
        string functionName,
        IReadOnlyList<CelBoundNode> arguments,
        CelFunctionOverload overload)
        : base(span, overload.ResultType)
    {
        Receiver = receiver;
        FunctionName = functionName;
        Arguments = arguments;
        Overload = overload;
    }
}
