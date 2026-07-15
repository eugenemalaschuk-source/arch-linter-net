namespace ArchLinterNet.CEL.Schema;

/// <summary>
/// Fluent builder for constructing an immutable <see cref="CelContextSchema"/>.
/// </summary>
public sealed class CelContextSchemaBuilder
{
    private readonly string _schemaId;
    private readonly List<CelVariable> _variables = [];
    private readonly HashSet<string> _variableNames = new(StringComparer.Ordinal);

    internal CelContextSchemaBuilder(string schemaId)
    {
        _schemaId = schemaId;
    }

    /// <summary>
    /// Declares a named variable of the given CEL type in this schema and returns its handle.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A variable with <paramref name="name"/> already exists, or <paramref name="name"/> is not
    /// a valid CEL identifier and could never be referenced from an expression.
    /// </exception>
    public CelVariable AddVariable(string name, CelType type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name must not be null or whitespace.", nameof(name));
        if (!CelIdentifier.IsValid(name))
            throw new ArgumentException(
                $"Variable name '{name}' is not a valid CEL identifier ([_a-zA-Z][_a-zA-Z0-9]*) " +
                "and could never be referenced from a Profile v1 expression.",
                nameof(name));
        ArgumentNullException.ThrowIfNull(type);
        if (!_variableNames.Add(name))
            throw new ArgumentException($"A variable named '{name}' has already been added to this schema.", nameof(name));

        var variable = new CelVariable(name, type);
        _variables.Add(variable);
        return variable;
    }

    /// <summary>
    /// Builds an immutable <see cref="CelContextSchema"/> from the variables declared so far.
    /// </summary>
    public CelContextSchema Build() => new(_schemaId, [.. _variables]);
}
