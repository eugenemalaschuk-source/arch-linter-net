using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Contracts.Validators;

internal static class PolicyDocumentValidatorSupport
{
    public static bool HasNonBlankEntry(IEnumerable<string> values)
    {
        return values.Any(value => !string.IsNullOrWhiteSpace(value));
    }

    // Eagerly validates every allowed_only_in_namespaces/forbidden_in_namespaces/
    // must_reside_in_namespaces entry against the same constrained glob grammar
    // layers.<name>.namespace uses (see NamespaceGlobPattern), mirroring how
    // LayerNamespacesValidator eagerly forces layer.GlobPattern at load time. Without this, an
    // unsupported wildcard pattern would compile silently and then simply never match any type
    // when the contract runs - a fail-open trap instead of a load-time configuration error.
    //
    // Blank/whitespace-only entries are rejected here too, not skipped: an empty string reaches
    // NamespaceGlobPattern.Parse unguarded as an "empty segment" once matching runs (throwing mid-
    // analysis instead of at load), and a whitespace-only entry parses as a harmless-looking
    // literal segment that can never match a real namespace - exactly the silent-no-match trap
    // this validator exists to close for wildcard patterns.
    public static void ValidateNamespacePatterns(string contractLabel, string fieldName, IEnumerable<string> entries)
    {
        foreach (string entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                throw new InvalidOperationException(
                    $"{contractLabel} '{fieldName}' contains a blank/whitespace-only entry. " +
                    "Every entry must be a non-blank namespace pattern.");
            }

            try
            {
                _ = NamespaceGlobPattern.Parse(entry);
            }
            catch (InvalidNamespacePatternException ex)
            {
                throw new InvalidOperationException($"{contractLabel} '{fieldName}': {ex.Message}", ex);
            }
        }
    }
}
