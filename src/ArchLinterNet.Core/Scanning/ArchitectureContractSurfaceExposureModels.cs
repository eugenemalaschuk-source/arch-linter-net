using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

[Flags]
internal enum ArchitectureContractSurfaceVisibilities
{
    None = 0,
    Public = 1 << 0,
    Protected = 1 << 1,
    ProtectedInternal = 1 << 2,
    Internal = 1 << 3,
    PrivateProtected = 1 << 4,
    Private = 1 << 5
}

// Source-compatible value façade for existing internal callers while the enum itself follows the
// plural naming convention required for a flags collection.
internal static class ArchitectureContractSurfaceVisibility
{
    internal const ArchitectureContractSurfaceVisibilities None = ArchitectureContractSurfaceVisibilities.None;
    internal const ArchitectureContractSurfaceVisibilities Public = ArchitectureContractSurfaceVisibilities.Public;
    internal const ArchitectureContractSurfaceVisibilities Protected = ArchitectureContractSurfaceVisibilities.Protected;
    internal const ArchitectureContractSurfaceVisibilities ProtectedInternal = ArchitectureContractSurfaceVisibilities.ProtectedInternal;
    internal const ArchitectureContractSurfaceVisibilities Internal = ArchitectureContractSurfaceVisibilities.Internal;
    internal const ArchitectureContractSurfaceVisibilities PrivateProtected = ArchitectureContractSurfaceVisibilities.PrivateProtected;
    internal const ArchitectureContractSurfaceVisibilities Private = ArchitectureContractSurfaceVisibilities.Private;
}

// A normalized visible-contract shape. Root membership is deliberately not modeled here: callers
// supply roots already selected by the reviewed API surface. The shape only describes which
// declared members and nested types contribute evidence for each selected root.
internal readonly record struct ArchitectureContractSurfaceShape
{
    private const ArchitectureContractSurfaceVisibilities AllVisibilities =
        ArchitectureContractSurfaceVisibilities.Public |
        ArchitectureContractSurfaceVisibilities.Protected |
        ArchitectureContractSurfaceVisibilities.ProtectedInternal |
        ArchitectureContractSurfaceVisibilities.Internal |
        ArchitectureContractSurfaceVisibilities.PrivateProtected |
        ArchitectureContractSurfaceVisibilities.Private;

    internal ArchitectureContractSurfaceShape(ArchitectureContractSurfaceVisibilities visibilities)
    {
        EnsureValid(visibilities);
        Visibilities = visibilities;
    }

    // Mirrors the public API surface used by #94/#525.
    internal static ArchitectureContractSurfaceShape Exported { get; } = new(
        ArchitectureContractSurfaceVisibilities.Public |
        ArchitectureContractSurfaceVisibilities.Protected |
        ArchitectureContractSurfaceVisibilities.ProtectedInternal);

    internal ArchitectureContractSurfaceVisibilities Visibilities { get; }

    internal void EnsureValid() => EnsureValid(Visibilities);

    internal bool Includes(MethodBase? method) => method != null && Includes(
        (method.Attributes & MethodAttributes.MemberAccessMask) switch
        {
            MethodAttributes.Private => ArchitectureContractSurfaceVisibilities.Private,
            MethodAttributes.FamANDAssem => ArchitectureContractSurfaceVisibilities.PrivateProtected,
            MethodAttributes.Assembly => ArchitectureContractSurfaceVisibilities.Internal,
            MethodAttributes.Family => ArchitectureContractSurfaceVisibilities.Protected,
            MethodAttributes.FamORAssem => ArchitectureContractSurfaceVisibilities.ProtectedInternal,
            MethodAttributes.Public => ArchitectureContractSurfaceVisibilities.Public,
            _ => ArchitectureContractSurfaceVisibilities.None
        });

    internal bool Includes(FieldInfo? field) => field != null && Includes(
        (field.Attributes & FieldAttributes.FieldAccessMask) switch
        {
            FieldAttributes.Private => ArchitectureContractSurfaceVisibilities.Private,
            FieldAttributes.FamANDAssem => ArchitectureContractSurfaceVisibilities.PrivateProtected,
            FieldAttributes.Assembly => ArchitectureContractSurfaceVisibilities.Internal,
            FieldAttributes.Family => ArchitectureContractSurfaceVisibilities.Protected,
            FieldAttributes.FamORAssem => ArchitectureContractSurfaceVisibilities.ProtectedInternal,
            FieldAttributes.Public => ArchitectureContractSurfaceVisibilities.Public,
            _ => ArchitectureContractSurfaceVisibilities.None
        });

    internal bool Includes(Type type) => Includes(
        (type.Attributes & TypeAttributes.VisibilityMask) switch
        {
            TypeAttributes.NotPublic or TypeAttributes.NestedAssembly => ArchitectureContractSurfaceVisibilities.Internal,
            TypeAttributes.Public or TypeAttributes.NestedPublic => ArchitectureContractSurfaceVisibilities.Public,
            TypeAttributes.NestedPrivate => ArchitectureContractSurfaceVisibilities.Private,
            TypeAttributes.NestedFamily => ArchitectureContractSurfaceVisibilities.Protected,
            TypeAttributes.NestedFamANDAssem => ArchitectureContractSurfaceVisibilities.PrivateProtected,
            TypeAttributes.NestedFamORAssem => ArchitectureContractSurfaceVisibilities.ProtectedInternal,
            _ => ArchitectureContractSurfaceVisibilities.None
        });

    private bool Includes(ArchitectureContractSurfaceVisibilities visibility) =>
        (Visibilities & visibility) != ArchitectureContractSurfaceVisibilities.None;

    private static void EnsureValid(ArchitectureContractSurfaceVisibilities visibilities)
    {
        if (visibilities == ArchitectureContractSurfaceVisibilities.None ||
            (visibilities & ~AllVisibilities) != ArchitectureContractSurfaceVisibilities.None)
        {
            throw new ArgumentOutOfRangeException(nameof(visibilities));
        }
    }
}

// A type identity used by the contract-surface evidence layer. AssemblyName contains
// Assembly.FullName, because full type names and simple assembly names alone are not enough:
// two resolved assemblies may legitimately contain the same namespace-qualified type name.
internal readonly record struct ArchitectureContractExposureTarget(
    string AssemblyName,
    string FullTypeName)
{
    public string Identity => $"{AssemblyName}:{FullTypeName}";
}

// Path segments intentionally keep their kind separate from their value. This lets consumers
// distinguish a member return from a generic argument or an attribute argument without parsing a
// display string, while values such as overloaded member signatures remain deterministic evidence.
internal readonly record struct ArchitectureContractExposurePathSegment(
    string Kind,
    string Value)
{
    public override string ToString() => Value.Length == 0 ? Kind : $"{Kind}:{Value}";
}

// Immutable, value-comparable path representation. IReadOnlyList<T> itself has reference equality,
// so the canonical key below is also the identity used for deduplication and stable ordering.
internal sealed class ArchitectureContractExposurePath : IEquatable<ArchitectureContractExposurePath>
{
    private readonly string _canonicalKey;

    public ArchitectureContractExposurePath(IEnumerable<ArchitectureContractExposurePathSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        Segments = Array.AsReadOnly(segments.ToArray());
        _canonicalKey = string.Join(
            "/",
            Segments.Select(segment =>
                $"{segment.Kind.Length}:{segment.Kind}{segment.Value.Length}:{segment.Value}"));
    }

    public static ArchitectureContractExposurePath Empty { get; } = new(Array.Empty<ArchitectureContractExposurePathSegment>());

    public IReadOnlyList<ArchitectureContractExposurePathSegment> Segments { get; }

    // A length-prefixed form avoids ambiguity if a member signature or metadata value itself
    // contains '/' or ':'. It is an implementation identity, not a user-facing diagnostic grammar.
    public string CanonicalKey => _canonicalKey;

    public ArchitectureContractExposurePath Append(string kind, string value = "")
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(value);
        return new ArchitectureContractExposurePath(
            Segments.Append(new ArchitectureContractExposurePathSegment(kind, value)));
    }

    public bool Equals(ArchitectureContractExposurePath? other) =>
        other != null && string.Equals(_canonicalKey, other._canonicalKey, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as ArchitectureContractExposurePath);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_canonicalKey);

    public override string ToString() => string.Join("/", Segments.Select(segment => segment.ToString()));
}

// One explainable route from a caller-selected visible root to a referenced type. DeclaringType is
// the supplied root, not an inferred policy subject; later consumers decide how to classify targets.
internal sealed record ArchitectureContractExposure(
    ArchitectureContractExposureTarget DeclaringType,
    ArchitectureContractExposurePath Path,
    ArchitectureContractExposureTarget ReferencedType)
{
    public ArchitectureContractExposureTarget SourceType => DeclaringType;

    public ArchitectureContractExposureTarget TargetType => ReferencedType;
}

// Reflection failures are first-class evidence. A missing member/type/attribute fact must never be
// represented by a silently shortened, apparently complete exposure graph.
internal sealed record ArchitectureContractExposureIncompleteEvidence(
    ArchitectureContractExposureTarget DeclaringType,
    ArchitectureContractExposurePath Path,
    string Reason)
{
    public ArchitectureContractExposureTarget SourceType => DeclaringType;
}

// Immutable session result. Exposures are deduplicated only when the complete source/path/target
// record is identical; separate paths to one target remain present.
internal sealed record ArchitectureContractSurfaceExposureResult(
    IReadOnlyList<ArchitectureContractExposure> Exposures,
    IReadOnlyList<ArchitectureContractExposureIncompleteEvidence> IncompleteEvidence)
{
    // Matching policy selectors against the reflected Type instance keeps external/framework
    // targets assessable. The stable ArchitectureContractExposureTarget remains the persisted
    // diagnostic identity; this map is an in-memory index used only during one analysis session.
    internal IReadOnlyDictionary<ArchitectureContractExposureTarget, Type> ReferencedTypes { get; init; } =
        new Dictionary<ArchitectureContractExposureTarget, Type>();

    public bool IsComplete => IncompleteEvidence.Count == 0;
}
