namespace ArchLinterNet.CEL;

/// <summary>
/// An immutable, typed handle for a variable declared in a <see cref="CelContextSchema"/>.
/// </summary>
/// <remarks>
/// <para>
/// Handles are returned by <see cref="CelContextSchemaBuilder.AddVariable"/> and used to set
/// values in <see cref="CelEvaluationContextBuilder.Set"/>. Using handles rather than string keys
/// avoids repeated string lookup in high-volume evaluation paths.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelVariable
{
    /// <summary>Gets the declared name of this variable.</summary>
    public string Name { get; }

    /// <summary>Gets the declared CEL type of this variable.</summary>
    public CelType Type { get; }

    internal CelVariable(string name, CelType type)
    {
        Name = name;
        Type = type;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Name}: {Type}";
}
