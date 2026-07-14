using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;

namespace ArchLinterNet.CEL.Evaluation;

/// <summary>
/// Fluent builder for constructing an immutable <see cref="CelEvaluationContext"/>.
/// </summary>
public sealed class CelEvaluationContextBuilder
{
    private readonly CelContextSchema _schema;
    private readonly Dictionary<CelVariable, CelValue> _assignments = new(ReferenceEqualityComparer.Instance);

    internal CelEvaluationContextBuilder(CelContextSchema schema)
    {
        _schema = schema;
    }

    /// <summary>
    /// Sets a variable's value using its typed handle.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="variable"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">The variable has already been set, or the value kind does not match the variable's declared type kind.</exception>
    public CelEvaluationContextBuilder Set(CelVariable variable, CelValue value)
    {
        ArgumentNullException.ThrowIfNull(variable);
        ArgumentNullException.ThrowIfNull(value);

        if (_assignments.ContainsKey(variable))
            throw new ArgumentException($"Variable '{variable.Name}' has already been set in this context.", nameof(variable));

        if (!KindsCompatible(variable.Type.Kind, value.Kind))
            throw new ArgumentException(
                $"Value kind {value.Kind} is not compatible with variable '{variable.Name}' declared as {variable.Type.Kind}.",
                nameof(value));

        _assignments[variable] = value;
        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="CelEvaluationContext"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">One or more required variables have not been set.</exception>
    public CelEvaluationContext Build()
    {
        var missing = _schema.Variables.Where(v => !_assignments.ContainsKey(v)).Select(v => v.Name).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"The following variables declared in schema '{_schema.SchemaId}' have not been set: {string.Join(", ", missing)}.");

        var assignments = _schema.Variables
            .Select(v => (v, _assignments[v]))
            .ToList();

        return new CelEvaluationContext(_schema, assignments);
    }

    private static bool KindsCompatible(CelTypeKind declared, CelValueKind actual) =>
        (declared, actual) switch
        {
            (CelTypeKind.Bool, CelValueKind.Bool) => true,
            (CelTypeKind.String, CelValueKind.String) => true,
            (CelTypeKind.Int, CelValueKind.Int) => true,
            (CelTypeKind.Float, CelValueKind.Float) => true,
            (CelTypeKind.List, CelValueKind.List) => true,
            (CelTypeKind.Map, CelValueKind.Map) => true,
            (CelTypeKind.Object, CelValueKind.Object) => true,
            _ => false,
        };
}
