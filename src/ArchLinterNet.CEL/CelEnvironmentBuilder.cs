using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;

namespace ArchLinterNet.CEL;

/// <summary>
/// Fluent builder for constructing an immutable <see cref="CelEnvironment"/>.
/// </summary>
public sealed class CelEnvironmentBuilder
{
    private readonly CelProfile _profile;
    private CelContextSchema? _schema;
    private CelCompilationLimits _limits = CelCompilationLimits.SafeDefaults;
    private CelEvaluationLimits _evaluationLimits = CelEvaluationLimits.SafeDefaults;
    private readonly Dictionary<string, CelObjectSchema> _objectSchemas = new(StringComparer.Ordinal);

    internal CelEnvironmentBuilder(CelProfile profile)
    {
        _profile = profile;
    }

    /// <summary>Attaches a context schema to this environment.</summary>
    public CelEnvironmentBuilder WithContextSchema(CelContextSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _schema = schema;
        return this;
    }

    /// <summary>Sets the compilation limits for this environment (default: <see cref="CelCompilationLimits.SafeDefaults"/>).</summary>
    public CelEnvironmentBuilder WithCompilationLimits(CelCompilationLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        return this;
    }

    /// <summary>
    /// Sets the environment-level evaluation limits (default: <see cref="CelEvaluationLimits.SafeDefaults"/>).
    /// Per-call evaluation limits passed to <c>Evaluate()</c> may not exceed these.
    /// </summary>
    public CelEnvironmentBuilder WithEvaluationLimits(CelEvaluationLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _evaluationLimits = limits;
        return this;
    }

    /// <summary>
    /// Registers an object type schema so the binder can resolve and type-check member access
    /// expressions without CLR reflection.
    /// </summary>
    /// <exception cref="ArgumentException">A schema for the same <see cref="CelObjectSchema.ObjectTypeId"/> has already been registered.</exception>
    public CelEnvironmentBuilder WithObjectSchema(CelObjectSchema objectSchema)
    {
        ArgumentNullException.ThrowIfNull(objectSchema);
        if (_objectSchemas.ContainsKey(objectSchema.ObjectTypeId))
            throw new ArgumentException(
                $"An object schema for type '{objectSchema.ObjectTypeId}' has already been registered.",
                nameof(objectSchema));
        _objectSchemas[objectSchema.ObjectTypeId] = objectSchema;
        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="CelEnvironment"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No context schema has been set.</exception>
    public CelEnvironment Build()
    {
        if (_schema is null)
            throw new InvalidOperationException(
                "A context schema is required. Call WithContextSchema before Build.");
        // Defensive copy: the builder must not be able to mutate an already-built environment
        // by calling WithObjectSchema() again after Build().
        var frozenSchemas = new System.Collections.ObjectModel.ReadOnlyDictionary<string, CelObjectSchema>(
            new Dictionary<string, CelObjectSchema>(_objectSchemas, StringComparer.Ordinal));
        return new CelEnvironment(_profile, _schema, _limits, _evaluationLimits, frozenSchemas);
    }
}
