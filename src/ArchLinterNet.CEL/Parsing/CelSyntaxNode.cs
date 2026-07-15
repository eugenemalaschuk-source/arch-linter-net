using ArchLinterNet.CEL.Diagnostics;

namespace ArchLinterNet.CEL.Parsing;

/// <summary>
/// Base type for an immutable internal syntax tree node produced by <see cref="CelParser"/>.
/// </summary>
/// <remarks>
/// Internal only — never exposed publicly. See the "Tooling and AST" extension-direction row in
/// <c>docs/internal/cel-engine-architecture.md</c>: a public/neutral syntax model is deferred
/// until a separate tooling story explicitly approves one.
/// </remarks>
internal abstract class CelSyntaxNode
{
    /// <summary>Gets the exact source span this node occupies.</summary>
    public CelSourceSpan Span { get; }

    protected CelSyntaxNode(CelSourceSpan span)
    {
        Span = span;
    }
}

/// <summary>A <c>bool</c> literal (<c>true</c>/<c>false</c>).</summary>
internal sealed class CelBoolLiteralSyntax : CelSyntaxNode
{
    public bool Value { get; }

    public CelBoolLiteralSyntax(CelSourceSpan span, bool value) : base(span) => Value = value;
}

/// <summary>A signed 64-bit integer literal.</summary>
internal sealed class CelIntLiteralSyntax : CelSyntaxNode
{
    public long Value { get; }

    public CelIntLiteralSyntax(CelSourceSpan span, long value) : base(span) => Value = value;
}

/// <summary>An IEEE 754 double-precision floating-point literal.</summary>
internal sealed class CelFloatLiteralSyntax : CelSyntaxNode
{
    public double Value { get; }

    public CelFloatLiteralSyntax(CelSourceSpan span, double value) : base(span) => Value = value;
}

/// <summary>A string literal, already escape-decoded.</summary>
internal sealed class CelStringLiteralSyntax : CelSyntaxNode
{
    public string Value { get; }

    public CelStringLiteralSyntax(CelSourceSpan span, string value) : base(span) => Value = value;
}

/// <summary>
/// A placeholder standing in for a deferred (valid-but-excluded-from-v1) CEL construct — e.g. an
/// arithmetic sub-expression, a conditional, a list/map/message literal, or a bare deferred
/// literal token. Carries only the span the deferred construct occupies. Never appears in a
/// successfully-compiled result: whenever the parser produces one, it also records a pending
/// <c>UnsupportedFeature</c> diagnostic that <see cref="CelParser.Parse"/> reports once the whole
/// expression has finished parsing (see design decision 17 — the pending-deferred model exists so
/// enclosing structural validation, such as a ternary's `:` or a call's closing `)`, still
/// completes before the deferred classification is decided, instead of short-circuiting on sight
/// of the first deferred token the way an immediate-throw design would).
/// </summary>
internal sealed class CelDeferredSyntax : CelSyntaxNode
{
    public CelDeferredSyntax(CelSourceSpan span) : base(span)
    {
    }
}

/// <summary>A bare identifier reference (a variable name to be resolved by the binder).</summary>
internal sealed class CelIdentifierSyntax : CelSyntaxNode
{
    public string Name { get; }

    public CelIdentifierSyntax(CelSourceSpan span, string name) : base(span) => Name = name;
}

/// <summary>A prefix unary expression, e.g. <c>!x</c>.</summary>
internal sealed class CelUnarySyntax : CelSyntaxNode
{
    public CelUnaryOperator Operator { get; }

    public CelSyntaxNode Operand { get; }

    public CelUnarySyntax(CelSourceSpan span, CelUnaryOperator @operator, CelSyntaxNode operand) : base(span)
    {
        Operator = @operator;
        Operand = operand;
    }
}

/// <summary>A binary expression, e.g. <c>a &amp;&amp; b</c> or <c>a == b</c>.</summary>
internal sealed class CelBinarySyntax : CelSyntaxNode
{
    public CelBinaryOperator Operator { get; }

    public CelSyntaxNode Left { get; }

    public CelSyntaxNode Right { get; }

    public CelBinarySyntax(CelSourceSpan span, CelBinaryOperator @operator, CelSyntaxNode left, CelSyntaxNode right) : base(span)
    {
        Operator = @operator;
        Left = left;
        Right = right;
    }
}

/// <summary>A dotted member access, e.g. <c>x.role</c>.</summary>
internal sealed class CelMemberAccessSyntax : CelSyntaxNode
{
    public CelSyntaxNode Receiver { get; }

    public string MemberName { get; }

    public CelMemberAccessSyntax(CelSourceSpan span, CelSyntaxNode receiver, string memberName) : base(span)
    {
        Receiver = receiver;
        MemberName = memberName;
    }
}

/// <summary>A bracketed index expression, e.g. <c>x[0]</c> or <c>m["key"]</c>.</summary>
internal sealed class CelIndexSyntax : CelSyntaxNode
{
    public CelSyntaxNode Receiver { get; }

    public CelSyntaxNode Index { get; }

    public CelIndexSyntax(CelSourceSpan span, CelSyntaxNode receiver, CelSyntaxNode index) : base(span)
    {
        Receiver = receiver;
        Index = index;
    }
}

/// <summary>
/// A function call, either a receiver call (<c>x.f(args)</c>, <see cref="Receiver"/> non-null) or
/// a free function call (<c>f(args)</c>, <see cref="Receiver"/> <c>null</c>). Whether the callee
/// resolves against Profile v1's function catalog is compile-time binder territory, not a parser
/// concern — see the tokenizer/parser implementation-scope requirement in the
/// <c>cel-profile-v1</c> spec.
/// </summary>
internal sealed class CelCallSyntax : CelSyntaxNode
{
    public CelSyntaxNode? Receiver { get; }

    public string FunctionName { get; }

    public IReadOnlyList<CelSyntaxNode> Arguments { get; }

    public CelCallSyntax(CelSourceSpan span, CelSyntaxNode? receiver, string functionName, IReadOnlyList<CelSyntaxNode> arguments) : base(span)
    {
        Receiver = receiver;
        FunctionName = functionName;
        // Defensive copy: the parser passes its working List<CelSyntaxNode> directly, which
        // callers could otherwise cast IReadOnlyList<> back to List<> and mutate the "immutable"
        // tree — same pattern as CelDiagnostic.Parameters / CelCompilationResult.Diagnostics.
        Arguments = new List<CelSyntaxNode>(arguments).AsReadOnly();
    }
}
