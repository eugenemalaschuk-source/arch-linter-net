namespace ArchLinterNet.Core.History.Git;

// Canonical ordering for paths, aliases, and dynamic map keys: lexicographic by Unicode scalar
// value, independent of UTF-16 code-unit order, locale collation, and normalization.
internal sealed class GitScalarValueComparer : IComparer<string>
{
    public static GitScalarValueComparer Instance { get; } = new();

    public int Compare(string? x, string? y) => GitPathDecoder.CompareScalarValue(x ?? string.Empty, y ?? string.Empty);
}
