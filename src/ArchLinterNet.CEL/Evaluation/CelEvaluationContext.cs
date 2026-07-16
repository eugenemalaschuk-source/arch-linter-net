using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;

namespace ArchLinterNet.CEL.Evaluation;

/// <summary>
/// An immutable activation that binds <see cref="CelVariable"/> handles to <see cref="CelValue"/>
/// instances for a single expression evaluation.
/// </summary>
/// <remarks>
/// <para>
/// Create via <see cref="CelContextSchema.CreateEvaluationContextBuilder"/>, then call
/// <see cref="CelEvaluationContextBuilder.Set"/> with the handles returned by
/// <see cref="CelContextSchemaBuilder.AddVariable"/>.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelEvaluationContext
{
    /// <summary>Gets the schema this context was built from.</summary>
    public CelContextSchema Schema { get; }

    /// <summary>
    /// Gets the structural schema identity captured when this context was built, including the
    /// environment object-schema catalog when one was available to the builder.
    /// </summary>
    internal string SchemaIdentity { get; }

    /// <summary>Gets the variable assignments in declaration order.</summary>
    public IReadOnlyList<(CelVariable Variable, CelValue Value)> Assignments { get; }

    /// <summary>
    /// Gets the immutable name lookup created with this context. Evaluation reuses this table so
    /// activation setup is not repeated for every compiled-program invocation.
    /// </summary>
    internal IReadOnlyDictionary<string, CelValue> ValuesByName { get; }

    internal CelEvaluationContext(
        CelContextSchema schema,
        string schemaIdentity,
        IReadOnlyList<(CelVariable, CelValue)> assignments)
    {
        Schema = schema;
        SchemaIdentity = schemaIdentity;
        // Copy to a truly frozen list so callers cannot cast Assignments back to List<> and mutate it.
        Assignments = new List<(CelVariable, CelValue)>(assignments).AsReadOnly();
        ValuesByName = assignments.ToDictionary(
            assignment => assignment.Item1.Name,
            assignment => assignment.Item2,
            StringComparer.Ordinal);
    }
}
