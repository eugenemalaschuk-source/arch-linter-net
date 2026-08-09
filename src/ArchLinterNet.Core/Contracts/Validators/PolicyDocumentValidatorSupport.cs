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
    public static void ValidateNamespacePatterns(string contractLabel, string fieldName, IEnumerable<string> entries)
    {
        foreach (string entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
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
