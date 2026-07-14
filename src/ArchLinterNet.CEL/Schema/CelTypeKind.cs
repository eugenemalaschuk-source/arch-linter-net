namespace ArchLinterNet.CEL.Schema;

/// <summary>
/// Identifies the kind of a <see cref="CelType"/> in ArchLinter CEL Profile v1.
/// </summary>
public enum CelTypeKind
{
    /// <summary>Boolean type (<c>bool</c> in CEL).</summary>
    Bool,

    /// <summary>String type (<c>string</c> in CEL).</summary>
    String,

    /// <summary>Signed 64-bit integer type (<c>int</c> in CEL).</summary>
    Int,

    /// <summary>IEEE 754 double-precision floating-point type (<c>double</c> in CEL).</summary>
    Float,

    /// <summary>Immutable homogeneous list type (<c>list</c> in CEL).</summary>
    List,

    /// <summary>Immutable string-keyed map type (<c>map</c> in CEL).</summary>
    Map,

    /// <summary>Schema-defined object type (no CLR reflection).</summary>
    Object,
}
