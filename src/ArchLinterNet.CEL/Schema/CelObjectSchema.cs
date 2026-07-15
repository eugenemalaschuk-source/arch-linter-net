using System.Text;

namespace ArchLinterNet.CEL.Schema;

/// <summary>
/// An immutable descriptor for a schema-defined object type, enumerating its named, typed members.
/// </summary>
/// <remarks>
/// <para>
/// Register object schemas via <see cref="CelEnvironmentBuilder.WithObjectSchema"/> so the
/// binder can resolve member-access expressions of the form <c>variable.memberName</c> and
/// verify their declared types without CLR reflection.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelObjectSchema
{
    /// <summary>Gets the type identifier matched against <see cref="CelType.ObjectOf"/> calls.</summary>
    public string ObjectTypeId { get; }

    /// <summary>Gets the declared members of this object type in declaration order.</summary>
    public IReadOnlyList<CelObjectMember> Members { get; }

    /// <summary>
    /// Gets a deterministic structural identity string derived from <see cref="ObjectTypeId"/>
    /// and member names and types. Included in <see cref="CelCompilationKey"/> schema identity.
    /// </summary>
    internal string Identity { get; }

    internal CelObjectSchema(string objectTypeId, IReadOnlyList<CelObjectMember> members)
    {
        ObjectTypeId = objectTypeId;
        // Copy to a truly frozen list so callers cannot cast Members back to T[] and mutate it.
        Members = new List<CelObjectMember>(members).AsReadOnly();
        Identity = ComputeIdentity(objectTypeId, Members);
    }

    private static string ComputeIdentity(string objectTypeId, IReadOnlyList<CelObjectMember> members)
    {
        var sb = new StringBuilder();
        sb.Append(objectTypeId.Length);
        sb.Append(':');
        sb.Append(objectTypeId);
        sb.Append('\0');
        sb.Append(members.Count);
        foreach (var m in members)
        {
            sb.Append('\0');
            sb.Append(m.Name.Length);
            sb.Append(':');
            sb.Append(m.Name);
            sb.Append(':');
            sb.Append(m.Type);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Creates a new <see cref="CelObjectSchemaBuilder"/> for the given object type identifier.
    /// </summary>
    public static CelObjectSchemaBuilder CreateBuilder(string objectTypeId)
    {
        if (string.IsNullOrWhiteSpace(objectTypeId))
            throw new ArgumentException("Object type ID must not be null or whitespace.", nameof(objectTypeId));
        return new CelObjectSchemaBuilder(objectTypeId);
    }
}
