namespace ArchLinterNet.Core.History.Evidence;

// Canonical text churn: raw LF-separated byte lines and the mathematical LCS length. Only the length
// matters, so the totals cannot depend on Myers/histogram/patience tie-breaking, Git attributes,
// textconv, or an external diff. Nothing here decodes file content to Unicode.
internal static class LineChurnCalculator
{
    public static (long Additions, long Deletions) Compute(byte[] oldContent, byte[] newContent)
    {
        IReadOnlyList<byte[]> oldLines = SplitLines(oldContent);
        IReadOnlyList<byte[]> newLines = SplitLines(newContent);
        long common = LongestCommonSubsequenceLength(oldLines, newLines);
        return (newLines.Count - common, oldLines.Count - common);
    }

    public static bool ContainsNul(byte[] content) => Array.IndexOf(content, (byte)0) >= 0;

    // LF terminates a line and is not payload. CR stays payload, empty content has zero lines, and a
    // terminal LF adds no extra trailing line.
    public static IReadOnlyList<byte[]> SplitLines(byte[] content)
    {
        List<byte[]> lines = [];
        int start = 0;
        while (start < content.Length)
        {
            int end = Array.IndexOf(content, (byte)'\n', start);
            if (end < 0)
            {
                lines.Add(content[start..]);
                break;
            }

            lines.Add(content[start..end]);
            start = end + 1;
        }

        return lines;
    }

    private static long LongestCommonSubsequenceLength(IReadOnlyList<byte[]> oldLines, IReadOnlyList<byte[]> newLines)
    {
        (int[] left, int[] right, long affix) = Reduce(oldLines, newLines);
        if (left.Length == 0 || right.Length == 0)
        {
            return affix;
        }

        // Two rows over the shorter sequence keeps memory proportional to min(n,m).
        if (left.Length < right.Length)
        {
            (left, right) = (right, left);
        }

        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];
        for (int outer = 1; outer <= left.Length; outer++)
        {
            int outerValue = left[outer - 1];
            for (int inner = 1; inner <= right.Length; inner++)
            {
                current[inner] = outerValue == right[inner - 1]
                    ? previous[inner - 1] + 1
                    : Math.Max(previous[inner], current[inner - 1]);
            }

            (previous, current) = (current, previous);
        }

        return affix + previous[right.Length];
    }

    // Lines are hashed to integers and the common prefix/suffix runs are removed. Both steps preserve
    // LCS length: identical leading and trailing runs always participate in some longest common
    // subsequence, and hash collisions are resolved by comparing the underlying bytes.
    private static (int[] Left, int[] Right, long Affix) Reduce(IReadOnlyList<byte[]> oldLines, IReadOnlyList<byte[]> newLines)
    {
        int prefix = 0;
        int limit = Math.Min(oldLines.Count, newLines.Count);
        while (prefix < limit && oldLines[prefix].AsSpan().SequenceEqual(newLines[prefix]))
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < limit - prefix
            && oldLines[oldLines.Count - 1 - suffix].AsSpan().SequenceEqual(newLines[newLines.Count - 1 - suffix]))
        {
            suffix++;
        }

        Dictionary<string, int> symbols = new(StringComparer.Ordinal);
        int[] left = Symbolize(oldLines, prefix, oldLines.Count - suffix, symbols);
        int[] right = Symbolize(newLines, prefix, newLines.Count - suffix, symbols);
        return (left, right, prefix + suffix);
    }

    private static int[] Symbolize(IReadOnlyList<byte[]> lines, int start, int end, Dictionary<string, int> symbols)
    {
        int[] result = new int[end - start];
        for (int index = start; index < end; index++)
        {
            string key = Convert.ToHexString(lines[index]);
            if (!symbols.TryGetValue(key, out int symbol))
            {
                symbol = symbols.Count;
                symbols[key] = symbol;
            }

            result[index - start] = symbol;
        }

        return result;
    }
}
