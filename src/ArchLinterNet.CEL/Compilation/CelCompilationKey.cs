using ArchLinterNet.CEL.Profile;

namespace ArchLinterNet.CEL.Compilation;

/// <summary>
/// An immutable, structurally-equal cache identity value for a compiled CEL expression.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CelCompilationKey"/> encodes all inputs that affect compilation output or the
/// captured runtime policy of the compiled program: expression source, profile identifier,
/// schema identity, required result type, compilation limits, and environment evaluation limits.
/// Two compilations with equal keys are semantically equivalent and may share a cached result.
/// </para>
/// <para>
/// Cache lifetime is caller-owned. <c>ArchLinterNet.CEL</c> does not maintain a process-global
/// cache or any mutable static state.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelCompilationKey : IEquatable<CelCompilationKey>
{
    /// <summary>
    /// Gets the expression source component of the identity. Currently this is the raw,
    /// unmodified source string: safe whitespace normalization requires tokenization (string
    /// literals must not be collapsed) and is reserved until the tokenizer (#325) lands.
    /// Until then, two sources differing only in whitespace produce different keys — a cache
    /// miss, never an incorrect hit. Any future normalization change alters cache identity and
    /// therefore requires a profile-version or documented key-format revision.
    /// </summary>
    public string NormalizedSource { get; }

    /// <summary>Gets the profile identifier used for compilation.</summary>
    public CelProfileId ProfileId { get; }

    /// <summary>Gets the structural identity of the context schema.</summary>
    public string SchemaIdentity { get; }

    /// <summary>Gets the required result type (predicate vs. general).</summary>
    public CelRequiredResultType RequiredResultType { get; }

    /// <summary>Gets the compilation-limits identity string.</summary>
    public string CompilationLimitsIdentity { get; }

    /// <summary>
    /// Gets the environment-level evaluation-limits identity string. Compiled programs capture
    /// their environment's evaluation ceiling, so two environments with different evaluation
    /// maximums must not share a cached program.
    /// </summary>
    public string EvaluationLimitsIdentity { get; }

    internal CelCompilationKey(
        string normalizedSource,
        CelProfileId profileId,
        string schemaIdentity,
        CelRequiredResultType requiredResultType,
        string compilationLimitsIdentity,
        string evaluationLimitsIdentity)
    {
        NormalizedSource = normalizedSource;
        ProfileId = profileId;
        SchemaIdentity = schemaIdentity;
        RequiredResultType = requiredResultType;
        CompilationLimitsIdentity = compilationLimitsIdentity;
        EvaluationLimitsIdentity = evaluationLimitsIdentity;
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
            && string.Equals(CompilationLimitsIdentity, other.CompilationLimitsIdentity, StringComparison.Ordinal)
            && string.Equals(EvaluationLimitsIdentity, other.EvaluationLimitsIdentity, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CelCompilationKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
        NormalizedSource,
        ProfileId,
        SchemaIdentity,
        RequiredResultType,
        CompilationLimitsIdentity,
        EvaluationLimitsIdentity);

    /// <inheritdoc/>
    public override string ToString() =>
        $"CelCompilationKey(profile={ProfileId}, resultType={RequiredResultType}, schema={SchemaIdentity})";
}
