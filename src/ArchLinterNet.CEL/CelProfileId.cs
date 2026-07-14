namespace ArchLinterNet.CEL;

/// <summary>
/// Immutable identifier for an ArchLinter CEL language profile.
/// </summary>
public readonly struct CelProfileId : IEquatable<CelProfileId>
{
    /// <summary>
    /// Gets the string value of this profile identifier.
    /// </summary>
    public string Value { get; }

    internal CelProfileId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Profile ID value must not be null or whitespace.", nameof(value));
        Value = value;
    }

    /// <summary>Implicitly converts a string to a <see cref="CelProfileId"/>.</summary>
    public static implicit operator CelProfileId(string value) => new(value);

    /// <inheritdoc/>
    public bool Equals(CelProfileId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CelProfileId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(CelProfileId left, CelProfileId right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(CelProfileId left, CelProfileId right) => !left.Equals(right);
}
