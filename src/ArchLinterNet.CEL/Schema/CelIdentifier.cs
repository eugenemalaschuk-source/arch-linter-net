namespace ArchLinterNet.CEL.Schema;

/// <summary>
/// Validates that schema-declared names are usable from Profile v1 expressions, per the pinned
/// CEL lexer: <c>IDENT = SELECTOR - RESERVED</c>, <c>SELECTOR = identifier-regex - KEYWORD</c>.
/// Variables must be full <c>IDENT</c>s (neither keywords nor reserved words); object members
/// only need to be <c>SELECTOR</c>s (not keywords — the CEL grammar permits reserved words in
/// member-access position).
/// </summary>
internal static class CelIdentifier
{
    // CEL keywords: token values with fixed meaning that can never be identifiers or selectors.
    private static readonly HashSet<string> _keywords = new(StringComparer.Ordinal)
    {
        "false", "in", "null", "true",
    };

    // CEL reserved identifiers: reserved for future use; invalid as plain identifiers but
    // permitted in selector (member-access) position.
    private static readonly HashSet<string> _reservedWords = new(StringComparer.Ordinal)
    {
        "as", "break", "const", "continue", "else", "for", "function", "if",
        "import", "let", "loop", "package", "namespace", "return", "var", "void", "while",
    };

    /// <summary>
    /// Returns whether <paramref name="name"/> is a valid CEL variable identifier
    /// (<c>IDENT</c>): matches <c>[_a-zA-Z][_a-zA-Z0-9]*</c> and is neither a CEL keyword nor a
    /// reserved identifier.
    /// </summary>
    internal static bool IsValidVariableName(string name) =>
        MatchesIdentifierGrammar(name) && !_keywords.Contains(name) && !_reservedWords.Contains(name);

    /// <summary>
    /// Returns whether <paramref name="name"/> is a valid CEL member-access selector
    /// (<c>SELECTOR</c>): matches <c>[_a-zA-Z][_a-zA-Z0-9]*</c> and is not a CEL keyword.
    /// Reserved identifiers are permitted in selector position per the CEL grammar.
    /// </summary>
    internal static bool IsValidMemberName(string name) =>
        MatchesIdentifierGrammar(name) && !_keywords.Contains(name);

    private static bool MatchesIdentifierGrammar(string name)
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
