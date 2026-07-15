namespace ArchLinterNet.CEL.Profile;

/// <summary>
/// Represents a versioned ArchLinter CEL language profile that pins the supported language subset,
/// semantics, and evaluation guarantees.
/// </summary>
/// <remarks>
/// <para>
/// ArchLinter CEL Profile v1 (<c>arch-linter/cel/v1</c>) pins the normative CEL language
/// specification at commit <c>59505c14f3187e6eb9684fbd3d07146f614c6148</c> of
/// https://github.com/cel-expr/cel-spec/blob/59505c14f3187e6eb9684fbd3d07146f614c6148/doc/langdef.md.
/// </para>
/// <para>
/// Profile v1 does NOT claim full CEL conformance. It defines a deliberate, bounded subset.
/// See the normative <c>cel-profile-v1</c> specification for the exact supported types,
/// operators, functions, and deferred features.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelProfile
{
    /// <summary>
    /// ArchLinter CEL Profile v1.
    /// Profile identifier: <c>arch-linter/cel/v1</c>.
    /// </summary>
    public static readonly CelProfile V1 = new(new CelProfileId("arch-linter/cel/v1"));

    /// <summary>Gets the identifier for this profile.</summary>
    public CelProfileId Id { get; }

    private CelProfile(CelProfileId id)
    {
        Id = id;
    }

    /// <inheritdoc/>
    public override string ToString() => Id.ToString();
}
