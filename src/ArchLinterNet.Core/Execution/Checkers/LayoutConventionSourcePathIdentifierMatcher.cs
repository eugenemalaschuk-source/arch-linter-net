using System.Text.RegularExpressions;

namespace ArchLinterNet.Core.Execution.Checkers;

// Preserves the source-generated identifier check used to identify CEL predicates whose safe
// evaluation requires source-path facts. This isolated generated-regex helper lets the file-level
// selector matcher remain non-partial.
internal static partial class LayoutConventionSourcePathIdentifierMatcher
{
    internal static bool References(string? when) =>
        !string.IsNullOrEmpty(when) && SourcePathIdentifierPattern().IsMatch(when);

    // Unconditional bare-word match, deliberately not a "smarter" syntax-aware check - mirrors
    // ExpressionCompilationValidator's DependencyIdentifierPattern and its documented rationale:
    // ArchLinterNet.CEL exposes no public API to introspect which identifiers a compiled predicate
    // references, and two prior attempts at hand-rolled CEL-lexical-grammar-aware string scanning
    // in this codebase each found a real bypass. A `when` referencing subject.sourcePaths or
    // subject.sourceDirectoryPrefixes against an empty-facts run would otherwise silently evaluate
    // to `false` for every candidate (an empty list, not an evaluation error) and produce a clean
    // pass that looks identical to "everything complies".
    [GeneratedRegex(@"\b(sourcePaths|sourceDirectoryPrefixes)\b", RegexOptions.CultureInvariant)]
    private static partial Regex SourcePathIdentifierPattern();
}
