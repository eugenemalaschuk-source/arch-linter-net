namespace ArchLinterNet.CEL.Schema;

/// <summary>
/// Fluent builder for constructing an immutable <see cref="CelObjectSchema"/>.
/// </summary>
public sealed class CelObjectSchemaBuilder
{
    private readonly string _objectTypeId;
    private readonly List<CelObjectMember> _members = [];
    private readonly HashSet<string> _memberNames = new(StringComparer.Ordinal);

    internal CelObjectSchemaBuilder(string objectTypeId)
    {
        _objectTypeId = objectTypeId;
    }

    /// <summary>
    /// Declares a named, typed member and returns its handle.
    /// </summary>
    /// <exception cref="ArgumentException">A member with the same name has already been declared.</exception>
    public CelObjectMember AddMember(string name, CelType type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Member name must not be null or whitespace.", nameof(name));
        ArgumentNullException.ThrowIfNull(type);
        if (!_memberNames.Add(name))
            throw new ArgumentException(
                $"A member named '{name}' has already been declared on object type '{_objectTypeId}'.",
                nameof(name));

        var member = new CelObjectMember(name, type);
        _members.Add(member);
        return member;
    }

    /// <summary>Builds an immutable <see cref="CelObjectSchema"/>.</summary>
    public CelObjectSchema Build() => new(_objectTypeId, [.. _members]);
}
