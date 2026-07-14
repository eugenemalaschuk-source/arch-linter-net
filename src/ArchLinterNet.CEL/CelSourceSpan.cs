namespace ArchLinterNet.CEL;

/// <summary>
/// Identifies a character-offset range within an expression source string.
/// </summary>
public readonly struct CelSourceSpan : IEquatable<CelSourceSpan>
{
    /// <summary>Gets the inclusive start offset (zero-based character index).</summary>
    public int Start { get; }

    /// <summary>Gets the exclusive end offset (zero-based character index).</summary>
    public int End { get; }

    /// <summary>
    /// Initializes a new <see cref="CelSourceSpan"/>.
    /// </summary>
    public CelSourceSpan(int start, int end)
    {
        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start), "Start must be non-negative.");
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), "End must be greater than or equal to Start.");
        Start = start;
        End = end;
    }

    /// <inheritdoc/>
    public bool Equals(CelSourceSpan other) => Start == other.Start && End == other.End;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CelSourceSpan other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Start, End);

    /// <inheritdoc/>
    public override string ToString() => $"[{Start}..{End})";

    /// <summary>Equality operator.</summary>
    public static bool operator ==(CelSourceSpan left, CelSourceSpan right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(CelSourceSpan left, CelSourceSpan right) => !left.Equals(right);
}
