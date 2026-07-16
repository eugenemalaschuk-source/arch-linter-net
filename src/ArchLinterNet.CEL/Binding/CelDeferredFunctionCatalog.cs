namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// Names of known-deferred CEL built-in functions that are valid CEL identifiers but excluded from
/// Profile v1. Calling one of these SHALL produce <c>UnsupportedFeature</c>, distinguishing it from
/// an entirely unknown name (<c>BindingError</c>), per the <c>cel-profile-v1</c> spec's built-in
/// function requirement: "The <c>matches</c> function and all regex, timestamp, duration, protobuf,
/// byte/string-conversion, and user-defined functions are deferred... (compile-time
/// <c>UnsupportedFeature</c> when the name is a known-deferred CEL built-in, <c>BindingError</c>
/// otherwise)." Only <c>matches</c> is pinned to a concrete name by the spec — the other deferred
/// categories (regex beyond <c>matches</c>, timestamp/duration/protobuf/conversion functions) are
/// described generically, not enumerated by name, so adding further names here without a spec
/// update would invent requirements the spec does not state.
/// </summary>
internal static class CelDeferredFunctionCatalog
{
    private static readonly HashSet<string> _knownDeferredNames = new(StringComparer.Ordinal)
    {
        "matches",
    };

    public static bool IsKnownDeferred(string functionName) => _knownDeferredNames.Contains(functionName);
}
