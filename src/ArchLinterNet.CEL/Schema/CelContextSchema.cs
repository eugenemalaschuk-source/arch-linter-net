using System.Text;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Evaluation;

namespace ArchLinterNet.CEL.Schema;

/// <summary>
/// An immutable, structurally-identified schema that declares the variables available in a
/// <see cref="CelEvaluationContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// Schemas are identified structurally: two schemas built with the same variable names and types
/// in the same order produce identical <see cref="Identity"/> strings.
/// </para>
/// <para>This class is immutable and thread-safe.</para>
/// </remarks>
public sealed class CelContextSchema
{
    /// <summary>Gets the logical identifier for this schema.</summary>
    public string SchemaId { get; }

    /// <summary>Gets the variables declared in this schema, in declaration order.</summary>
    public IReadOnlyList<CelVariable> Variables { get; }

    /// <summary>
    /// Gets a deterministic structural identity string derived from <see cref="SchemaId"/>,
    /// variable names, and their type kinds. Suitable for use in <see cref="CelCompilationKey"/>.
    /// </summary>
    public string Identity { get; }

    internal CelContextSchema(string schemaId, IReadOnlyList<CelVariable> variables)
    {
        SchemaId = schemaId;
        Variables = variables;
        Identity = ComputeIdentity(schemaId, variables);
    }

    private static string ComputeIdentity(string schemaId, IReadOnlyList<CelVariable> variables)
    {
        var sb = new StringBuilder();
        sb.Append(schemaId);
        foreach (var v in variables)
        {
            sb.Append('|');
            sb.Append(v.Name);
            sb.Append(':');
            sb.Append(v.Type);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Creates a new <see cref="CelContextSchemaBuilder"/> with the given schema identifier.
    /// </summary>
    public static CelContextSchemaBuilder CreateBuilder(string schemaId)
    {
        if (string.IsNullOrWhiteSpace(schemaId))
            throw new ArgumentException("Schema ID must not be null or whitespace.", nameof(schemaId));
        return new CelContextSchemaBuilder(schemaId);
    }

    /// <summary>
    /// Creates a new <see cref="CelEvaluationContextBuilder"/> bound to this schema.
    /// </summary>
    public CelEvaluationContextBuilder CreateEvaluationContextBuilder() =>
        new(this);

    /// <inheritdoc/>
    public override string ToString() => SchemaId;
}
