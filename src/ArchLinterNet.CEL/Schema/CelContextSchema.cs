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
    /// Gets a deterministic, collision-safe structural identity string derived from
    /// <see cref="SchemaId"/>, variable names, and their type descriptors.
    /// Suitable for use in <see cref="CelCompilationKey"/>.
    /// </summary>
    public string Identity { get; }

    internal CelContextSchema(string schemaId, IReadOnlyList<CelVariable> variables)
    {
        SchemaId = schemaId;
        // Copy to a truly frozen list so callers cannot cast Variables back to T[] and mutate it.
        Variables = new List<CelVariable>(variables).AsReadOnly();
        Identity = ComputeIdentity(schemaId, Variables);
    }

    internal string ComputeEnvironmentIdentity(IReadOnlyDictionary<string, CelObjectSchema>? objectSchemas)
    {
        if (objectSchemas is null || objectSchemas.Count == 0)
            return Identity;

        var sb = new StringBuilder(Identity);
        foreach (var kv in objectSchemas.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            sb.Append('\0');
            sb.Append(kv.Value.Identity);
        }

        return sb.ToString();
    }

    private static string ComputeIdentity(string schemaId, IReadOnlyList<CelVariable> variables)
    {
        // Length-prefixed, null-byte-separated encoding prevents delimiter-collision between
        // a schemaId containing separator chars and a schemaId with variables that spell the same
        // string when naively concatenated.  Example collision the old "|"-only format allowed:
        //   SchemaId="x|a:string", 0 vars  →  old: "x|a:string"
        //   SchemaId="x",  var a:string    →  old: "x|a:string"  (same!)
        // New format:
        //   SchemaId="x|a:string", 0 vars  →  "10:x|a:string\00"
        //   SchemaId="x",  var a:string    →  "1:x\01\01:a:string"
        var sb = new StringBuilder();
        sb.Append(schemaId.Length);
        sb.Append(':');
        sb.Append(schemaId);
        sb.Append('\0');
        sb.Append(variables.Count);
        foreach (var v in variables)
        {
            sb.Append('\0');
            sb.Append(v.Name.Length);
            sb.Append(':');
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
        new(this, null);

    /// <summary>
    /// Creates a new <see cref="CelEvaluationContextBuilder"/> bound to this schema, with access
    /// to the given object schema catalog for full composite-type and member-type validation.
    /// </summary>
    internal CelEvaluationContextBuilder CreateEvaluationContextBuilder(
        IReadOnlyDictionary<string, CelObjectSchema> objectSchemas) =>
        new(this, objectSchemas);

    /// <inheritdoc/>
    public override string ToString() => SchemaId;
}
