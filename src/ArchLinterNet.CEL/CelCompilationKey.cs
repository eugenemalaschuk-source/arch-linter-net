namespace ArchLinterNet.CEL;

/// <summary>
/// An immutable, structurally-equal cache identity value for a compiled CEL expression.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CelCompilationKey"/> encodes all inputs that affect compilation output:
/// normalized source, profile identifier, schema identity, required result type, and
/// compilation limits. Two compilations with equal keys are semantically equivalent and may
/// share a cached result.
/// </para>
/// <para>
/// Cache lifetime is caller-owned. <c>ArchLinterNet.CEL</c> does not maintain a process-global
/// cache or any mutable static state.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelCompilationKey : IEquatable<CelCompilationKey>
{
    /// <summary>Gets the normalized (whitespace-collapsed) expression source.</summary>
    public string NormalizedSource { get; }

    /// <summary>Gets the profile identifier used for compilation.</summary>
    public CelProfileId ProfileId { get; }

    /// <summary>Gets the structural identity of the context schema.</summary>
    public string SchemaIdentity { get; }

    /// <summary>Gets the required result type (predicate vs. general).</summary>
    public CelRequiredResultType RequiredResultType { get; }

    /// <summary>Gets the compilation-limits identity string.</summary>
    public string CompilationLimitsIdentity { get; }

    internal CelCompilationKey(
        string normalizedSource,
        CelProfileId profileId,
        string schemaIdentity,
        CelRequiredResultType requiredResultType,
        string compilationLimitsIdentity)
    {
        NormalizedSource = normalizedSource;
        ProfileId = profileId;
        SchemaIdentity = schemaIdentity;
        RequiredResultType = requiredResultType;
        CompilationLimitsIdentity = compilationLimitsIdentity;
    }

    /// <inheritdoc/>
    public bool Equals(CelCompilationKey? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(NormalizedSource, other.NormalizedSource, StringComparison.Ordinal)
            && ProfileId == other.ProfileId
            && string.Equals(SchemaIdentity, other.SchemaIdentity, StringComparison.Ordinal)
            && RequiredResultType == other.RequiredResultType
            && string.Equals(CompilationLimitsIdentity, other.CompilationLimitsIdentity, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CelCompilationKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
        NormalizedSource,
        ProfileId,
        SchemaIdentity,
        RequiredResultType,
        CompilationLimitsIdentity);

    /// <inheritdoc/>
    public override string ToString() =>
        $"CelCompilationKey(profile={ProfileId}, resultType={RequiredResultType}, schema={SchemaIdentity})";
}
