using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;

namespace ArchLinterNet.CEL.Evaluation;

/// <summary>
/// Fluent builder for constructing an immutable <see cref="CelEvaluationContext"/>.
/// </summary>
public sealed class CelEvaluationContextBuilder
{
    private readonly CelContextSchema _schema;
    private readonly IReadOnlyDictionary<string, CelObjectSchema>? _objectSchemas;
    private readonly Dictionary<CelVariable, CelValue> _assignments = new(ReferenceEqualityComparer.Instance);

    // Cap structural validation depth and collection size so deeply-nested or extremely large
    // CelValues cannot cause stack overflow or unbounded CPU use via the public Set() path
    // before any evaluation limits apply.
    private const int MaxValidationDepth = 16;
    private const int MaxValidationCollectionSize = 1024;

    internal CelEvaluationContextBuilder(
        CelContextSchema schema,
        IReadOnlyDictionary<string, CelObjectSchema>? objectSchemas)
    {
        _schema = schema;
        _objectSchemas = objectSchemas;
    }

    /// <summary>
    /// Sets a variable's value using its typed handle.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="variable"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The variable handle does not belong to this schema; the variable has already been set; or
    /// the value kind does not structurally match the variable's declared type.
    /// </exception>
    public CelEvaluationContextBuilder Set(CelVariable variable, CelValue value)
    {
        ArgumentNullException.ThrowIfNull(variable);
        ArgumentNullException.ThrowIfNull(value);

        if (!_schema.Variables.Any(v => ReferenceEquals(v, variable)))
            throw new ArgumentException(
                $"Variable '{variable.Name}' is not declared in schema '{_schema.SchemaId}'. " +
                "Only handles returned by this schema's builder are valid.",
                nameof(variable));

        if (_assignments.ContainsKey(variable))
            throw new ArgumentException(
                $"Variable '{variable.Name}' has already been set in this context.",
                nameof(variable));

        if (!ValueMatchesType(variable.Type, value, depth: 0))
            throw new ArgumentException(
                $"Value kind {value.Kind} is not structurally compatible with variable " +
                $"'{variable.Name}' declared as {variable.Type}.",
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

    private bool ValueMatchesType(CelType declared, CelValue actual, int depth)
    {
        if (depth > MaxValidationDepth) return false;
        return (declared.Kind, actual.Kind) switch
        {
            (CelTypeKind.Bool, CelValueKind.Bool) => true,
            (CelTypeKind.String, CelValueKind.String) => true,
            (CelTypeKind.Int, CelValueKind.Int) => true,
            (CelTypeKind.Float, CelValueKind.Float) => true,
            (CelTypeKind.List, CelValueKind.List) => ValidateListElements(declared, actual, depth + 1),
            (CelTypeKind.Map, CelValueKind.Map) => ValidateMapValues(declared, actual, depth + 1),
            (CelTypeKind.Object, CelValueKind.Object) => ValidateObjectValue(declared, actual.AsObject(), depth + 1),
            _ => false,
        };
    }

    private bool ValidateListElements(CelType declared, CelValue listValue, int depth)
    {
        var elements = listValue.AsList();
        if (elements.Count > MaxValidationCollectionSize) return false;
        if (declared.ElementType is null) return true;
        return elements.All(el => ValueMatchesType(declared.ElementType, el, depth));
    }

    private bool ValidateMapValues(CelType declared, CelValue mapValue, int depth)
    {
        var map = mapValue.AsMap();
        if (map.Count > MaxValidationCollectionSize) return false;
        if (declared.ValueType is null) return true;
        return map.Values.All(v => ValueMatchesType(declared.ValueType, v, depth));
    }

    private bool ValidateObjectValue(CelType declared, CelObjectValue obj, int depth)
    {
        if (obj.ObjectTypeId != declared.SchemaId) return false;
        if (obj.Members.Count > MaxValidationCollectionSize) return false;

        if (_objectSchemas is null || !_objectSchemas.TryGetValue(obj.ObjectTypeId, out var objSchema))
            return true;

        foreach (var (memberName, memberValue) in obj.Members)
        {
            var memberDef = objSchema.Members.FirstOrDefault(m => m.Name == memberName);
            if (memberDef is null) return false;
            if (!ValueMatchesType(memberDef.Type, memberValue, depth)) return false;
        }
        return true;
    }
}
