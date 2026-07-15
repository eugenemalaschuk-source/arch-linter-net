namespace ArchLinterNet.CEL.Parsing;

/// <summary>Binary operators supported by Profile v1.</summary>
internal enum CelBinaryOperator
{
    /// <summary>Logical conjunction (<c>&amp;&amp;</c>).</summary>
    And,

    /// <summary>Logical disjunction (<c>||</c>).</summary>
    Or,

    /// <summary>Equality (<c>==</c>).</summary>
    Equal,

    /// <summary>Inequality (<c>!=</c>).</summary>
    NotEqual,

    /// <summary>Ordered less-than (<c>&lt;</c>).</summary>
    Less,

    /// <summary>Ordered less-than-or-equal (<c>&lt;=</c>).</summary>
    LessOrEqual,

    /// <summary>Ordered greater-than (<c>&gt;</c>).</summary>
    Greater,

    /// <summary>Ordered greater-than-or-equal (<c>&gt;=</c>).</summary>
    GreaterOrEqual,

    /// <summary>Set/key membership (<c>in</c>).</summary>
    In,
}
