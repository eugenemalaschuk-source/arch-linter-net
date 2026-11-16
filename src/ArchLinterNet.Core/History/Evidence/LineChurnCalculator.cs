namespace ArchLinterNet.Core.History.Evidence;

// Canonical text churn: raw LF-separated byte lines and the mathematical LCS length. Only the length
// matters, so the totals cannot depend on Myers/histogram/patience tie-breaking, Git attributes,
// textconv, or an external diff. Nothing here decodes file content to Unicode.
internal static class LineChurnCalculator
{
    private const int MaxMyersEditDistance = 32;

    public static (long Additions, long Deletions) Compute(byte[] oldContent, byte[] newContent)
    {
        return ComputeCore(oldContent, newContent, collectWork: false, out _);
    }

    internal static (long Additions, long Deletions) ComputeWithWork(
        byte[] oldContent,
        byte[] newContent,
        out WorkCounts work)
    {
        return ComputeCore(oldContent, newContent, collectWork: true, out work);
    }

    private static (long Additions, long Deletions) ComputeCore(
        byte[] oldContent,
        byte[] newContent,
        bool collectWork,
        out WorkCounts work)
    {
        IReadOnlyList<byte[]> oldLines = SplitLines(oldContent);
        IReadOnlyList<byte[]> newLines = SplitLines(newContent);
        long common = LongestCommonSubsequenceLength(oldLines, newLines, collectWork, out work);
        return (newLines.Count - common, oldLines.Count - common);
    }

    public static bool ContainsNul(byte[] content)
    {
        return Array.IndexOf(content, (byte)0) >= 0;
    }

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

    private static long LongestCommonSubsequenceLength(
        IReadOnlyList<byte[]> oldLines,
        IReadOnlyList<byte[]> newLines,
        bool collectWork,
        out WorkCounts work)
    {
        (int[] left, int[] right, long affix) = Reduce(oldLines, newLines);
        if (left.Length == 0 || right.Length == 0)
        {
            work = default;
            return affix;
        }

        bool completedMyers = TryLongestCommonSubsequenceByMyers(
            left,
            right,
            collectWork,
            out int myersLcsLength,
            out long frontierSteps,
            out long lineComparisons,
            out int frontierArrayLength);
        if (completedMyers)
        {
            work = new WorkCounts(
                UsedMyers: true,
                UsedFallback: false,
                FrontierSteps: frontierSteps,
                LineComparisons: lineComparisons,
                DynamicProgrammingCells: 0,
                FrontierArrayLength: frontierArrayLength,
                DynamicProgrammingRowLength: 0);
            return affix + myersLcsLength;
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

        work = new WorkCounts(
            UsedMyers: false,
            UsedFallback: true,
            FrontierSteps: frontierSteps,
            LineComparisons: lineComparisons,
            DynamicProgrammingCells: collectWork ? (long)left.Length * right.Length : 0,
            FrontierArrayLength: frontierArrayLength,
            DynamicProgrammingRowLength: right.Length + 1);
        return affix + previous[right.Length];
    }

    // Only the shortest insertion/deletion distance is needed to calculate LCS length, so this
    // forward frontier does not retain the trace (which would grow quadratically on dense inputs).
    // A bounded search caps edit-distance exploration and keeps frontier storage independent of input size.
    private static bool TryLongestCommonSubsequenceByMyers(
        int[] left,
        int[] right,
        bool collectWork,
        out int lcsLength,
        out long frontierSteps,
        out long lineComparisons,
        out int frontierArrayLength)
    {
        lcsLength = 0;
        frontierSteps = 0;
        lineComparisons = 0;
        frontierArrayLength = 0;
        if (Math.Abs((long)left.Length - right.Length) > MaxMyersEditDistance)
        {
            return false;
        }

        int[] furthestXByDiagonal = new int[(2 * MaxMyersEditDistance) + 3];
        frontierArrayLength = furthestXByDiagonal.Length;
        int offset = MaxMyersEditDistance + 1;
        furthestXByDiagonal[offset + 1] = 0;

        for (int distance = 0; distance <= MaxMyersEditDistance; distance++)
        {
            for (int diagonal = -distance; diagonal <= distance; diagonal += 2)
            {
                if (collectWork)
                {
                    frontierSteps++;
                }

                int x = diagonal == -distance ||
                    (diagonal != distance && furthestXByDiagonal[offset + diagonal - 1] < furthestXByDiagonal[offset + diagonal + 1])
                    ? furthestXByDiagonal[offset + diagonal + 1]
                    : furthestXByDiagonal[offset + diagonal - 1] + 1;
                int y = x - diagonal;
                while (x >= 0 && x < left.Length && y >= 0 && y < right.Length)
                {
                    if (collectWork)
                    {
                        lineComparisons++;
                    }

                    if (left[x] != right[y])
                    {
                        break;
                    }

                    x++;
                    y++;
                }

                furthestXByDiagonal[offset + diagonal] = x;
                if (x >= left.Length && y >= right.Length)
                {
                    lcsLength = (int)(((long)left.Length + right.Length - distance) / 2);
                    return true;
                }
            }
        }

        return false;
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

    internal readonly record struct WorkCounts(
        bool UsedMyers,
        bool UsedFallback,
        long FrontierSteps,
        long LineComparisons,
        long DynamicProgrammingCells,
        int FrontierArrayLength,
        int DynamicProgrammingRowLength);
}
