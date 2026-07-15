namespace ArchLinterNet.CEL.Schema;

/// <summary>
/// Validates that schema-declared names are usable as CEL identifiers, so a variable or object
/// member declared through the schema API can always be referenced from a Profile v1 expression.
/// </summary>
internal static class CelIdentifier
{
    /// <summary>
    /// Returns whether <paramref name="name"/> matches the CEL identifier grammar:
    /// <c>[_a-zA-Z][_a-zA-Z0-9]*</c>.
    /// </summary>
    internal static bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (!IsIdentifierStart(name[0])) return false;
        for (var i = 1; i < name.Length; i++)
        {
            if (!IsIdentifierPart(name[i])) return false;
        }
        return true;
    }

    private static bool IsIdentifierStart(char c) =>
        c == '_' || c is >= 'a' and <= 'z' || c is >= 'A' and <= 'Z';

    private static bool IsIdentifierPart(char c) =>
        IsIdentifierStart(c) || c is >= '0' and <= '9';
}
