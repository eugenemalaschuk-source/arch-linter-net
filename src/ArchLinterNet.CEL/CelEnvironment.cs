namespace ArchLinterNet.CEL;

/// <summary>
/// An immutable, thread-safe CEL evaluation environment that can compile and reuse expressions
/// over a shared context schema.
/// </summary>
/// <remarks>
/// <para>
/// Build via <see cref="CreateBuilder"/>. After construction, no mutation or function
/// registration is possible. One environment may be shared across multiple compilations and
/// threads without synchronization.
/// </para>
/// <para>
/// The normal compile path always parses, binds, and type-checks the whole expression.
/// Parse-only and partial-check paths do not exist in the public API.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelEnvironment
{
    /// <summary>Gets the language profile active in this environment.</summary>
    public CelProfile Profile { get; }

    /// <summary>Gets the context schema this environment is bound to.</summary>
    public CelContextSchema Schema { get; }

    /// <summary>Gets the compilation limits active in this environment.</summary>
    public CelCompilationLimits CompilationLimits { get; }

    internal CelEnvironment(CelProfile profile, CelContextSchema schema, CelCompilationLimits limits)
    {
        Profile = profile;
        Schema = schema;
        CompilationLimits = limits;
    }

    /// <summary>
    /// Creates a new <see cref="CelEnvironmentBuilder"/> for the given profile.
    /// </summary>
    public static CelEnvironmentBuilder CreateBuilder(CelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new CelEnvironmentBuilder(profile);
    }

    /// <summary>
    /// Compiles the given expression source as a boolean predicate, returning a structured result.
    /// </summary>
    /// <remarks>
    /// Invalid user expressions produce structured diagnostics — no exception is thrown.
    /// Programmer misuse (null source) throws immediately.
    /// </remarks>
    public CelCompilationResult<CelCompiledPredicate> CompilePredicate(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var key = BuildKey(source, CelRequiredResultType.Predicate);
        return CelCompilationResult<CelCompiledPredicate>.NotYetImplemented(key);
    }

    /// <summary>
    /// Compiles the given expression source as a general expression returning any CEL value.
    /// </summary>
    /// <remarks>
    /// Invalid user expressions produce structured diagnostics — no exception is thrown.
    /// Programmer misuse (null source) throws immediately.
    /// </remarks>
    public CelCompilationResult<CelCompiledExpression> Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var key = BuildKey(source, CelRequiredResultType.General);
        return CelCompilationResult<CelCompiledExpression>.NotYetImplemented(key);
    }

    private CelCompilationKey BuildKey(string source, CelRequiredResultType resultType)
    {
        var normalized = NormalizeSource(source);
        return new CelCompilationKey(
            normalizedSource: normalized,
            profileId: Profile.Id,
            schemaIdentity: Schema.Identity,
            requiredResultType: resultType,
            compilationLimitsIdentity: CompilationLimits.ComputeIdentity());
    }

    private static string NormalizeSource(string source) =>
        string.Join(' ', source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
