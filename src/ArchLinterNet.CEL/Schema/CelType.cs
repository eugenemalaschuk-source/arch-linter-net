namespace ArchLinterNet.CEL.Schema;

/// <summary>
/// Describes a CEL type used to declare variables and schema members in ArchLinter CEL Profile v1.
/// </summary>
/// <remarks>
/// <para>
/// Use the static properties and factory methods to obtain type descriptors.
/// Object types are defined through <see cref="CelContextSchema"/> — never through CLR reflection.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelType
{
    /// <summary>Gets the kind of this type.</summary>
    public CelTypeKind Kind { get; }

    /// <summary>
    /// Gets the element type for <see cref="CelTypeKind.List"/> types; <c>null</c> otherwise.
    /// </summary>
    public CelType? ElementType { get; }

    /// <summary>
    /// Gets the value type for <see cref="CelTypeKind.Map"/> types; <c>null</c> otherwise.
    /// </summary>
    public CelType? ValueType { get; }

    /// <summary>
    /// Gets the schema identifier for <see cref="CelTypeKind.Object"/> types; <c>null</c> otherwise.
    /// </summary>
    public string? SchemaId { get; }

    private CelType(CelTypeKind kind, CelType? elementType = null, CelType? valueType = null, string? schemaId = null)
    {
        Kind = kind;
        ElementType = elementType;
        ValueType = valueType;
        SchemaId = schemaId;
    }

    /// <summary>The CEL <c>bool</c> type.</summary>
    public static CelType Bool { get; } = new(CelTypeKind.Bool);

    /// <summary>The CEL <c>string</c> type.</summary>
    public static CelType String { get; } = new(CelTypeKind.String);

    /// <summary>The CEL <c>int</c> type (signed 64-bit integer).</summary>
    public static CelType Int { get; } = new(CelTypeKind.Int);

    /// <summary>The CEL <c>double</c> type (IEEE 754 double-precision floating-point).</summary>
    public static CelType Float { get; } = new(CelTypeKind.Float);

    /// <summary>
    /// Returns a CEL <c>list</c> type descriptor with the given element type.
    /// </summary>
    public static CelType ListOf(CelType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return new CelType(CelTypeKind.List, elementType: elementType);
    }

    /// <summary>
    /// Returns a CEL <c>map</c> type descriptor with string keys and the given value type.
    /// </summary>
    public static CelType MapOf(CelType valueType)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        return new CelType(CelTypeKind.Map, valueType: valueType);
    }

    /// <summary>
    /// Returns a CEL object type descriptor identified by the given schema identifier.
    /// Object shape is defined through <see cref="CelContextSchema"/>; no CLR reflection occurs.
    /// </summary>
    public static CelType ObjectOf(string schemaId)
    {
        if (string.IsNullOrWhiteSpace(schemaId))
            throw new ArgumentException("Schema ID must not be null or whitespace.", nameof(schemaId));
        return new CelType(CelTypeKind.Object, schemaId: schemaId);
    }

    /// <inheritdoc/>
    public override string ToString() => Kind switch
    {
        CelTypeKind.List => $"list<{ElementType}>",
        CelTypeKind.Map => $"map<string, {ValueType}>",
        CelTypeKind.Object => $"object({SchemaId})",
        _ => Kind.ToString().ToLowerInvariant(),
    };
}
