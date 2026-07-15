namespace ArchLinterNet.CEL.Values;

/// <summary>
/// An immutable CEL value produced by evaluating an expression or supplied as context input.
/// </summary>
/// <remarks>
/// <para>
/// Use the static factory methods to create values. Typed accessor methods throw
/// <see cref="InvalidOperationException"/> when the kind does not match.
/// No arbitrary CLR objects, <c>dynamic</c>, or reflection-based inputs are accepted.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelValue
{
    /// <summary>Gets the runtime kind of this value.</summary>
    public CelValueKind Kind { get; }

    private readonly bool _boolValue;
    private readonly string? _stringValue;
    private readonly long _intValue;
    private readonly double _floatValue;
    private readonly IReadOnlyList<CelValue>? _listValue;
    private readonly IReadOnlyDictionary<string, CelValue>? _mapValue;
    private readonly CelObjectValue? _objectValue;

    // Split into one constructor per kind (instead of a single constructor with 7 defaulted
    // scalar/collection parameters) to stay within Sonar's constructor-arity limit (S107).
    private CelValue(CelValueKind kind, bool boolValue)
    {
        Kind = kind;
        _boolValue = boolValue;
    }

    private CelValue(CelValueKind kind, string stringValue)
    {
        Kind = kind;
        _stringValue = stringValue;
    }

    private CelValue(CelValueKind kind, long intValue)
    {
        Kind = kind;
        _intValue = intValue;
    }

    private CelValue(CelValueKind kind, double floatValue)
    {
        Kind = kind;
        _floatValue = floatValue;
    }

    private CelValue(CelValueKind kind, IReadOnlyList<CelValue> listValue)
    {
        Kind = kind;
        _listValue = listValue;
    }

    private CelValue(CelValueKind kind, IReadOnlyDictionary<string, CelValue> mapValue)
    {
        Kind = kind;
        _mapValue = mapValue;
    }

    private CelValue(CelValueKind kind, CelObjectValue objectValue)
    {
        Kind = kind;
        _objectValue = objectValue;
    }

    /// <summary>Creates a CEL <c>bool</c> value.</summary>
    public static CelValue Bool(bool value) =>
        new(CelValueKind.Bool, value);

    /// <summary>Creates a CEL <c>string</c> value.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> contains an unpaired UTF-16 surrogate. CEL strings are sequences
    /// of Unicode code points; malformed UTF-16 does not represent valid code points and cannot
    /// be a CEL string value.
    /// </exception>
    public static CelValue String(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsWellFormedUtf16(value))
            throw new ArgumentException(
                "String contains an unpaired UTF-16 surrogate and does not represent a valid " +
                "sequence of Unicode code points.",
                nameof(value));
        return new CelValue(CelValueKind.String, value);
    }

    private static bool IsWellFormedUtf16(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1])) return false;
                i++;
            }
            else if (char.IsLowSurrogate(c))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Creates a CEL <c>int</c> value (signed 64-bit).</summary>
    public static CelValue Int(long value) =>
        new(CelValueKind.Int, value);

    /// <summary>Creates a CEL <c>double</c> value.</summary>
    public static CelValue Float(double value) =>
        new(CelValueKind.Float, value);

    /// <summary>Creates an immutable CEL list value. Defensively copies the input.</summary>
    public static CelValue List(IReadOnlyList<CelValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CelValue(CelValueKind.List, new List<CelValue>(value).AsReadOnly());
    }

    /// <summary>Creates an immutable, string-keyed CEL map value. Defensively copies the input.</summary>
    public static CelValue Map(IReadOnlyDictionary<string, CelValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CelValue(CelValueKind.Map,
            new Dictionary<string, CelValue>(value, StringComparer.Ordinal).AsReadOnly());
    }

    /// <summary>Creates a schema-defined CEL object value.</summary>
    public static CelValue Object(CelObjectValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CelValue(CelValueKind.Object, value);
    }

    /// <summary>Returns the boolean value. Throws <see cref="InvalidOperationException"/> if kind is not <see cref="CelValueKind.Bool"/>.</summary>
    public bool AsBool() => Kind == CelValueKind.Bool
        ? _boolValue
        : throw new InvalidOperationException($"Cannot read Bool from a CelValue of kind {Kind}.");

    /// <summary>Returns the string value. Throws <see cref="InvalidOperationException"/> if kind is not <see cref="CelValueKind.String"/>.</summary>
    public string AsString() => Kind == CelValueKind.String && _stringValue is not null
        ? _stringValue
        : throw new InvalidOperationException($"Cannot read String from a CelValue of kind {Kind}.");

    /// <summary>Returns the integer value. Throws <see cref="InvalidOperationException"/> if kind is not <see cref="CelValueKind.Int"/>.</summary>
    public long AsInt() => Kind == CelValueKind.Int
        ? _intValue
        : throw new InvalidOperationException($"Cannot read Int from a CelValue of kind {Kind}.");

    /// <summary>Returns the float value. Throws <see cref="InvalidOperationException"/> if kind is not <see cref="CelValueKind.Float"/>.</summary>
    public double AsFloat() => Kind == CelValueKind.Float
        ? _floatValue
        : throw new InvalidOperationException($"Cannot read Float from a CelValue of kind {Kind}.");

    /// <summary>Returns the list value. Throws <see cref="InvalidOperationException"/> if kind is not <see cref="CelValueKind.List"/>.</summary>
    public IReadOnlyList<CelValue> AsList() => Kind == CelValueKind.List && _listValue is not null
        ? _listValue
        : throw new InvalidOperationException($"Cannot read List from a CelValue of kind {Kind}.");

    /// <summary>Returns the map value. Throws <see cref="InvalidOperationException"/> if kind is not <see cref="CelValueKind.Map"/>.</summary>
    public IReadOnlyDictionary<string, CelValue> AsMap() => Kind == CelValueKind.Map && _mapValue is not null
        ? _mapValue
        : throw new InvalidOperationException($"Cannot read Map from a CelValue of kind {Kind}.");

    /// <summary>Returns the object value. Throws <see cref="InvalidOperationException"/> if kind is not <see cref="CelValueKind.Object"/>.</summary>
    public CelObjectValue AsObject() => Kind == CelValueKind.Object && _objectValue is not null
        ? _objectValue
        : throw new InvalidOperationException($"Cannot read Object from a CelValue of kind {Kind}.");

    /// <inheritdoc/>
    public override string ToString() => Kind switch
    {
        CelValueKind.Bool => _boolValue.ToString().ToLowerInvariant(),
        CelValueKind.String => $"\"{_stringValue}\"",
        CelValueKind.Int => _intValue.ToString(),
        CelValueKind.Float => _floatValue.ToString("G"),
        CelValueKind.List => "list[...]",
        CelValueKind.Map => "map{...}",
        CelValueKind.Object => _objectValue?.ToString() ?? "object",
        _ => Kind.ToString(),
    };
}
