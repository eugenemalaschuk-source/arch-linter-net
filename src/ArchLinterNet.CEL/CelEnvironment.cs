using System.Text;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Parsing;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;

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

    /// <summary>
    /// Gets the environment-level evaluation limits. Per-call limits passed to
    /// <c>Evaluate()</c> may tighten but must not exceed these maximums.
    /// </summary>
    public CelEvaluationLimits EvaluationLimits { get; }

    /// <summary>
    /// Gets the registered object type schemas, keyed by <see cref="CelObjectSchema.ObjectTypeId"/>.
    /// The binder uses these to resolve and type-check member access expressions.
    /// </summary>
    public IReadOnlyDictionary<string, CelObjectSchema> ObjectSchemas { get; }

    internal CelEnvironment(
        CelProfile profile,
        CelContextSchema schema,
        CelCompilationLimits limits,
        CelEvaluationLimits evaluationLimits,
        IReadOnlyDictionary<string, CelObjectSchema> objectSchemas)
    {
        Profile = profile;
        Schema = schema;
        CompilationLimits = limits;
        EvaluationLimits = evaluationLimits;
        ObjectSchemas = objectSchemas;
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
    /// Creates an evaluation context builder pre-loaded with this environment's object schema
    /// catalog for full composite-type and member-type validation.
    /// </summary>
    public CelEvaluationContextBuilder CreateEvaluationContextBuilder() =>
        Schema.CreateEvaluationContextBuilder(ObjectSchemas);

    /// <summary>
    /// Compiles the given expression source as a boolean predicate, returning a structured result.
    /// </summary>
    /// <remarks>
    /// Invalid user expressions produce structured diagnostics — no exception is thrown.
    /// Programmer misuse (null source) throws immediately.
    /// <see cref="CelCompilationLimits.MaxExpressionLength"/> is enforced before any tokenization.
    /// </remarks>
    public CelCompilationResult<CelCompiledPredicate> CompilePredicate(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var key = BuildKey(source, CelRequiredResultType.Predicate);
        if (source.Length > CompilationLimits.MaxExpressionLength)
            return CelCompilationResult<CelCompiledPredicate>.BudgetExceeded(key);
        var parseError = TryParse(source);
        if (parseError is not null)
            return new CelCompilationResult<CelCompiledPredicate>(false, null, [parseError], key);
        return CelCompilationResult<CelCompiledPredicate>.NotYetImplemented(key);
    }

    /// <summary>
    /// Compiles the given expression source as a general expression returning any CEL value.
    /// </summary>
    /// <remarks>
    /// Invalid user expressions produce structured diagnostics — no exception is thrown.
    /// Programmer misuse (null source) throws immediately.
    /// <see cref="CelCompilationLimits.MaxExpressionLength"/> is enforced before any tokenization.
    /// </remarks>
    public CelCompilationResult<CelCompiledExpression> Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var key = BuildKey(source, CelRequiredResultType.General);
        if (source.Length > CompilationLimits.MaxExpressionLength)
            return CelCompilationResult<CelCompiledExpression>.BudgetExceeded(key);
        var parseError = TryParse(source);
        if (parseError is not null)
            return new CelCompilationResult<CelCompiledExpression>(false, null, [parseError], key);
        return CelCompilationResult<CelCompiledExpression>.NotYetImplemented(key);
    }

    /// <summary>
    /// Runs the tokenizer and parser over <paramref name="source"/>. Returns <c>null</c> when the
    /// expression is syntactically valid Profile v1 CEL (binding/type-checking is #326's scope);
    /// otherwise returns the single diagnostic explaining the syntax error, unsupported-feature
    /// condition, or structural-limit violation encountered.
    /// </summary>
    private CelDiagnostic? TryParse(string source)
    {
        var tokenizeResult = CelTokenizer.Tokenize(source, CompilationLimits, Profile.Id);
        if (!tokenizeResult.IsSuccess)
            return tokenizeResult.Diagnostic;
        var parseResult = CelParser.Parse(tokenizeResult.Tokens, CompilationLimits, Profile.Id);
        return parseResult.IsSuccess ? null : parseResult.Diagnostic;
    }

    private CelCompilationKey BuildKey(string source, CelRequiredResultType resultType) =>
        new(
            normalizedSource: source,
            profileId: Profile.Id,
            schemaIdentity: ComputeSchemaIdentity(),
            requiredResultType: resultType,
            compilationLimitsIdentity: CompilationLimits.ComputeIdentity(),
            evaluationLimitsIdentity: EvaluationLimits.ComputeIdentity());

    private string ComputeSchemaIdentity()
    {
        if (ObjectSchemas.Count == 0)
            return Schema.Identity;
        var sb = new StringBuilder(Schema.Identity);
        foreach (var kv in ObjectSchemas.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            sb.Append('\0');
            sb.Append(kv.Value.Identity);
        }
        return sb.ToString();
    }
}
