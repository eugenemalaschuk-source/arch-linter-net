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
    /// <exception cref="ArgumentException">A variable with <paramref name="name"/> already exists.</exception>
    public CelVariable AddVariable(string name, CelType type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name must not be null or whitespace.", nameof(name));
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
