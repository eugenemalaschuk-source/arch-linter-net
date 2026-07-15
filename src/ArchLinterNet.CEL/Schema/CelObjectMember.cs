namespace ArchLinterNet.CEL.Schema;

/// <summary>
/// A typed member handle for a field declared in a <see cref="CelObjectSchema"/>.
/// </summary>
/// <remarks>
/// Returned by <see cref="CelObjectSchemaBuilder.AddMember"/>. The binder uses these handles
/// to resolve and type-check member access expressions of the form <c>variable.memberName</c>
/// without CLR reflection.
/// </remarks>
public sealed class CelObjectMember
{
    /// <summary>Gets the member name as it appears in CEL member-access expressions.</summary>
    public string Name { get; }

    /// <summary>Gets the declared CEL type of this member.</summary>
    public CelType Type { get; }

    internal CelObjectMember(string name, CelType type)
    {
        Name = name;
        Type = type;
    }
}
