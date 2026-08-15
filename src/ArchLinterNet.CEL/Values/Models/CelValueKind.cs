namespace ArchLinterNet.CEL.Values;

/// <summary>
/// Identifies the runtime kind of a <see cref="CelValue"/> instance.
/// </summary>
public enum CelValueKind
{
    /// <summary>Boolean value.</summary>
    Bool,

    /// <summary>String value.</summary>
    String,

    /// <summary>Signed 64-bit integer value.</summary>
    Int,

    /// <summary>IEEE 754 double-precision floating-point value.</summary>
    Float,

    /// <summary>Immutable list value.</summary>
    List,

    /// <summary>Immutable string-keyed map value.</summary>
    Map,

    /// <summary>Schema-defined object value.</summary>
    Object,
}
