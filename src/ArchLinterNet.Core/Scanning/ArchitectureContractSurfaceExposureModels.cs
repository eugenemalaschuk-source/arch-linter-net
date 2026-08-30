using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

[Flags]
internal enum ArchitectureContractSurfaceVisibility
{
    None = 0,
    Public = 1 << 0,
    Protected = 1 << 1,
    ProtectedInternal = 1 << 2,
    Internal = 1 << 3,
    PrivateProtected = 1 << 4,
    Private = 1 << 5
}

// A normalized visible-contract shape. Root membership is deliberately not modeled here: callers
// supply roots already selected by the reviewed API surface. The shape only describes which
// declared members and nested types contribute evidence for each selected root.
internal readonly record struct ArchitectureContractSurfaceShape
{
    private const ArchitectureContractSurfaceVisibility AllVisibilities =
        ArchitectureContractSurfaceVisibility.Public |
        ArchitectureContractSurfaceVisibility.Protected |
        ArchitectureContractSurfaceVisibility.ProtectedInternal |
        ArchitectureContractSurfaceVisibility.Internal |
        ArchitectureContractSurfaceVisibility.PrivateProtected |
        ArchitectureContractSurfaceVisibility.Private;

    internal ArchitectureContractSurfaceShape(ArchitectureContractSurfaceVisibility visibilities)
    {
        EnsureValid(visibilities);
        Visibilities = visibilities;
    }

    // Mirrors the public API surface used by #94/#525.
    internal static ArchitectureContractSurfaceShape Exported { get; } = new(
        ArchitectureContractSurfaceVisibility.Public |
        ArchitectureContractSurfaceVisibility.Protected |
        ArchitectureContractSurfaceVisibility.ProtectedInternal);

    internal ArchitectureContractSurfaceVisibility Visibilities { get; }

    internal void EnsureValid() => EnsureValid(Visibilities);

    internal bool Includes(MethodBase? method) => method != null && Includes(
        (method.Attributes & MethodAttributes.MemberAccessMask) switch
        {
            MethodAttributes.Private => ArchitectureContractSurfaceVisibility.Private,
            MethodAttributes.FamANDAssem => ArchitectureContractSurfaceVisibility.PrivateProtected,
            MethodAttributes.Assembly => ArchitectureContractSurfaceVisibility.Internal,
            MethodAttributes.Family => ArchitectureContractSurfaceVisibility.Protected,
            MethodAttributes.FamORAssem => ArchitectureContractSurfaceVisibility.ProtectedInternal,
            MethodAttributes.Public => ArchitectureContractSurfaceVisibility.Public,
            _ => ArchitectureContractSurfaceVisibility.None
        });

    internal bool Includes(FieldInfo? field) => field != null && Includes(
        (field.Attributes & FieldAttributes.FieldAccessMask) switch
        {
            FieldAttributes.Private => ArchitectureContractSurfaceVisibility.Private,
            FieldAttributes.FamANDAssem => ArchitectureContractSurfaceVisibility.PrivateProtected,
            FieldAttributes.Assembly => ArchitectureContractSurfaceVisibility.Internal,
            FieldAttributes.Family => ArchitectureContractSurfaceVisibility.Protected,
            FieldAttributes.FamORAssem => ArchitectureContractSurfaceVisibility.ProtectedInternal,
            FieldAttributes.Public => ArchitectureContractSurfaceVisibility.Public,
            _ => ArchitectureContractSurfaceVisibility.None
        });

    internal bool Includes(Type type) => Includes(
        (type.Attributes & TypeAttributes.VisibilityMask) switch
        {
            TypeAttributes.NotPublic or TypeAttributes.NestedAssembly => ArchitectureContractSurfaceVisibility.Internal,
            TypeAttributes.Public or TypeAttributes.NestedPublic => ArchitectureContractSurfaceVisibility.Public,
            TypeAttributes.NestedPrivate => ArchitectureContractSurfaceVisibility.Private,
            TypeAttributes.NestedFamily => ArchitectureContractSurfaceVisibility.Protected,
            TypeAttributes.NestedFamANDAssem => ArchitectureContractSurfaceVisibility.PrivateProtected,
            TypeAttributes.NestedFamORAssem => ArchitectureContractSurfaceVisibility.ProtectedInternal,
            _ => ArchitectureContractSurfaceVisibility.None
        });

    private bool Includes(ArchitectureContractSurfaceVisibility visibility) =>
        (Visibilities & visibility) != ArchitectureContractSurfaceVisibility.None;

    private static void EnsureValid(ArchitectureContractSurfaceVisibility visibilities)
    {
        if (visibilities == ArchitectureContractSurfaceVisibility.None ||
            (visibilities & ~AllVisibilities) != ArchitectureContractSurfaceVisibility.None)
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
    public bool IsComplete => IncompleteEvidence.Count == 0;
}
