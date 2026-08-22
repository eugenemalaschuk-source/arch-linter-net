namespace ArchLinterNet.Core.History.Canonical;

// Canonical string ordering for History evidence, configuration, and reports: lexicographic by
// Unicode scalar value, independent of UTF-16 code-unit order, locale collation, and normalization.
// This is deliberately outside raw Git decoding because finalized report rendering needs the same
// ordering without importing the Git ingestion implementation namespace.
internal static class HistoryScalarValueComparer
{
    public static IComparer<string> Instance { get; } = Comparer<string>.Create(Compare);

    public static int Compare(string? left, string? right)
    {
        string first = left ?? string.Empty;
        string second = right ?? string.Empty;
        int firstIndex = 0;
        int secondIndex = 0;
        while (firstIndex < first.Length && secondIndex < second.Length)
        {
            int firstScalar = NextScalar(first, ref firstIndex);
            int secondScalar = NextScalar(second, ref secondIndex);
            if (firstScalar != secondScalar)
            {
                return firstScalar < secondScalar ? -1 : 1;
            }
        }

        return (first.Length - firstIndex).CompareTo(second.Length - secondIndex);
    }

    private static int NextScalar(string value, ref int index)
    {
        char current = value[index];
        if (char.IsHighSurrogate(current) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
        {
            int scalar = char.ConvertToUtf32(current, value[index + 1]);
            index += 2;
            return scalar;
        }

        index++;
        return current;
    }
}
