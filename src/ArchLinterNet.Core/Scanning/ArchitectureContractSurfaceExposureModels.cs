namespace ArchLinterNet.Core.Scanning;

// A type identity used by the contract-surface evidence layer. Full names alone are not enough:
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
