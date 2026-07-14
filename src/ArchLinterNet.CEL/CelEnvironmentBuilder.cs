using ArchLinterNet.CEL.Compilation;
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
    /// Builds an immutable <see cref="CelEnvironment"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No context schema has been set.</exception>
    public CelEnvironment Build()
    {
        if (_schema is null)
            throw new InvalidOperationException(
                "A context schema is required. Call WithContextSchema before Build.");
        return new CelEnvironment(_profile, _schema, _limits);
    }
}
