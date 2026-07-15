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

    // Per-collection caps alone do not bound total work: a shallow structure such as
    // List[1024] of List[1024] of Int keeps every individual collection within
    // MaxValidationCollectionSize while still visiting 1M+ value nodes. This is a separate,
    // cumulative budget on the total number of CelValue nodes visited across one Set() call's
    // recursive traversal, threaded through as a ref counter so every nested call shares it.
    // Deliberately an internal Profile v1 constant (not a CelEvaluationLimits field): like
    // MaxValidationDepth/MaxValidationCollectionSize above, this bounds the synchronous,
    // programmer-facing Set() validation path that runs before any compiled program exists,
    // not the per-call evaluation budget that CelEvaluationLimits governs. If a future profile
    // version makes this caller-configurable, it must become part of CelEvaluationLimits and be
    // folded into CelEvaluationLimits.ComputeIdentity() / CelCompilationKey.EvaluationLimitsIdentity.
    private const int MaxValidationNodeCount = 10_000;

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

        var nodeCount = 0;
        if (!ValueMatchesType(variable.Type, value, depth: 0, ref nodeCount))
            throw new ArgumentException(
                $"Value kind {value.Kind} is not structurally compatible with variable " +
                $"'{variable.Name}' declared as {variable.Type}, or the value exceeds the maximum " +
                "structural validation depth, collection size, or total node budget.",
                nameof(value));

        _assignments[variable] = value;
        return this;
    }

    /// <summary>
    /// Sets a variable's value by declared name. Ergonomic convenience over
    /// <see cref="Set(CelVariable, CelValue)"/>: resolves <paramref name="name"/> to its schema
    /// handle via a single lookup, then delegates to the handle-based overload. Prefer the
    /// handle-based overload in high-volume evaluation paths to avoid repeated string lookup.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// No variable named <paramref name="name"/> is declared in this schema; the variable has
    /// already been set; or the value kind does not structurally match the variable's declared type.
    /// </exception>
    public CelEvaluationContextBuilder Set(string name, CelValue value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        var variable = _schema.Variables.FirstOrDefault(v => v.Name == name)
            ?? throw new ArgumentException(
                $"No variable named '{name}' is declared in schema '{_schema.SchemaId}'.",
                nameof(name));

        return Set(variable, value);
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

    private bool ValueMatchesType(CelType declared, CelValue actual, int depth, ref int nodeCount)
    {
        // Defensive guard: every public factory (CelValue.List/Map, CelObjectValue) already
        // rejects null members at construction, but a null here must fail closed rather than
        // throw NullReferenceException if that invariant is ever violated upstream.
        if (actual is null) return false;
        if (depth > MaxValidationDepth) return false;

        nodeCount++;
        if (nodeCount > MaxValidationNodeCount) return false;

        return (declared.Kind, actual.Kind) switch
        {
            (CelTypeKind.Bool, CelValueKind.Bool) => true,
            (CelTypeKind.String, CelValueKind.String) => true,
            (CelTypeKind.Int, CelValueKind.Int) => true,
            (CelTypeKind.Float, CelValueKind.Float) => true,
            (CelTypeKind.List, CelValueKind.List) => ValidateListElements(declared, actual, depth + 1, ref nodeCount),
            (CelTypeKind.Map, CelValueKind.Map) => ValidateMapValues(declared, actual, depth + 1, ref nodeCount),
            (CelTypeKind.Object, CelValueKind.Object) => ValidateObjectValue(declared, actual.AsObject(), depth + 1, ref nodeCount),
            _ => false,
        };
    }

    private bool ValidateListElements(CelType declared, CelValue listValue, int depth, ref int nodeCount)
    {
        var elements = listValue.AsList();
        if (elements.Count > MaxValidationCollectionSize) return false;
        if (declared.ElementType is null) return true;

        // A plain loop (not LINQ .All) is required here: nodeCount is threaded by ref through the
        // entire recursive traversal, and ref locals cannot be captured by a lambda closure.
        foreach (var el in elements)
        {
            if (!ValueMatchesType(declared.ElementType, el, depth, ref nodeCount)) return false;
        }
        return true;
    }

    private bool ValidateMapValues(CelType declared, CelValue mapValue, int depth, ref int nodeCount)
    {
        var map = mapValue.AsMap();
        if (map.Count > MaxValidationCollectionSize) return false;
        if (declared.ValueType is null) return true;

        foreach (var v in map.Values)
        {
            if (!ValueMatchesType(declared.ValueType, v, depth, ref nodeCount)) return false;
        }
        return true;
    }

    private bool ValidateObjectValue(CelType declared, CelObjectValue obj, int depth, ref int nodeCount)
    {
        if (obj.ObjectTypeId != declared.SchemaId) return false;
        if (obj.Members.Count > MaxValidationCollectionSize) return false;

        // Object values can only be validated against a registered object schema. A builder
        // without a catalog (created via schema.CreateEvaluationContextBuilder()) must not
        // silently accept unvalidated objects — that would let a context violate the schema
        // invariant that every declared member is present with a value of its declared type.
        if (_objectSchemas is null)
            throw new InvalidOperationException(
                $"Cannot validate object value of type '{obj.ObjectTypeId}': this evaluation " +
                "context builder has no object schema catalog. Create the builder via " +
                "CelEnvironment.CreateEvaluationContextBuilder() so registered object schemas " +
                "are available for member validation.");

        if (!_objectSchemas.TryGetValue(obj.ObjectTypeId, out var objSchema))
            return false;

        // Profile v1 has no null/optional members: the member set must match the schema exactly.
        // A missing declared member or an extra undeclared member both reject the value.
        if (obj.Members.Count != objSchema.Members.Count) return false;

        foreach (var memberDef in objSchema.Members)
        {
            if (!obj.Members.TryGetValue(memberDef.Name, out var memberValue)) return false;
            if (!ValueMatchesType(memberDef.Type, memberValue, depth, ref nodeCount)) return false;
        }
        return true;
    }
}
