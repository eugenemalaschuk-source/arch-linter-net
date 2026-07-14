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

    /// <summary>Gets the variable assignments in declaration order.</summary>
    public IReadOnlyList<(CelVariable Variable, CelValue Value)> Assignments { get; }

    internal CelEvaluationContext(CelContextSchema schema, IReadOnlyList<(CelVariable, CelValue)> assignments)
    {
        Schema = schema;
        Assignments = assignments;
    }
}
