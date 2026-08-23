using System.Globalization;

namespace ArchLinterNet.GitFuzz;

internal static class FuzzCorpus
{
    private const string SourceDirectoryName = "corpus-source";

    internal static IReadOnlyList<string> Materialize(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        List<string> written = [];
        foreach (string sourcePath in Directory.EnumerateFiles(SourceDirectory, "*.hex").Order(StringComparer.Ordinal))
        {
            byte[] input = Decode(File.ReadAllText(sourcePath));
            string outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".bin");
            File.WriteAllBytes(outputPath, input);
            written.Add(outputPath);
        }

        return written;
    }

    private static string SourceDirectory
        => Path.Combine(AppContext.BaseDirectory, SourceDirectoryName);

    private static byte[] Decode(string text)
    {
        List<byte> bytes = [];
        foreach (string rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = StripComment(rawLine);
            foreach (string token in line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AppendToken(token, bytes);
            }
        }

        return [.. bytes];
    }

    private static string StripComment(string line)
    {
        int commentIndex = line.IndexOf('#', StringComparison.Ordinal);
        return commentIndex < 0 ? line : line[..commentIndex];
    }

    private static void AppendToken(string token, List<byte> bytes)
    {
        string[] repeat = token.Split('*', StringSplitOptions.TrimEntries);
        if (repeat.Length == 2)
        {
            byte value = ParseByte(repeat[0]);
            int count = int.Parse(repeat[1], NumberStyles.None, CultureInfo.InvariantCulture);
            for (int index = 0; index < count; index++)
            {
                bytes.Add(value);
            }

            return;
        }

        if (token.Length % 2 != 0)
        {
            throw new FormatException($"Corpus token '{token}' has an odd number of hex digits.");
        }

        for (int index = 0; index < token.Length; index += 2)
        {
            bytes.Add(ParseByte(token.Substring(index, 2)));
        }
    }

    private static byte ParseByte(string hex)
    {
        if (hex.Length != 2)
        {
            throw new FormatException($"Corpus byte token '{hex}' must contain exactly two hex digits.");
        }

        return byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
