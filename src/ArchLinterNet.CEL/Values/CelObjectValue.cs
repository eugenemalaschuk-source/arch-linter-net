namespace ArchLinterNet.CEL.Values;

/// <summary>
/// An immutable, schema-defined CEL object value with named, typed members.
/// </summary>
/// <remarks>
/// <para>
/// Object values are created via <see cref="CelValue.Object(CelObjectValue)"/>. Members are
/// accessed in CEL expressions using dot notation. No CLR reflection occurs.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelObjectValue
{
    /// <summary>Gets the schema identifier that describes this object's type.</summary>
    public string ObjectTypeId { get; }

    /// <summary>Gets the immutable, string-keyed member values of this object.</summary>
    public IReadOnlyDictionary<string, CelValue> Members { get; }

    /// <summary>
    /// Creates a new <see cref="CelObjectValue"/> with the given type identifier and members.
    /// Defensively copies <paramref name="members"/> so the caller cannot mutate the value after construction.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="members"/> contains a null value. Profile v1 defines no null CEL value.
    /// </exception>
    public CelObjectValue(string objectTypeId, IReadOnlyDictionary<string, CelValue> members)
    {
        if (string.IsNullOrWhiteSpace(objectTypeId))
            throw new ArgumentException("Object type ID must not be null or whitespace.", nameof(objectTypeId));
        ArgumentNullException.ThrowIfNull(members);

        var copy = new Dictionary<string, CelValue>(members.Count, StringComparer.Ordinal);
        foreach (var (key, memberValue) in members)
        {
            if (memberValue is null)
                throw new ArgumentException(
                    "Object member values must not be null. Profile v1 defines no null CEL value.",
                    nameof(members));
            copy[key] = memberValue;
        }

        ObjectTypeId = objectTypeId;
        Members = copy.AsReadOnly();
    }

    /// <inheritdoc/>
    public override string ToString() => $"object({ObjectTypeId})";
}
