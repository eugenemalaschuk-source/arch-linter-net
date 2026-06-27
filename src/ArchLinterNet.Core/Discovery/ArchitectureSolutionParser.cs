using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ArchLinterNet.Core.Discovery;

internal static class ArchitectureSolutionParser
{
    private static readonly Regex _classicSlnProjectLine = new(
        "^Project\\(\"\\{[^}]*}\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"([^\"]*)\",\\s*\"\\{[^}]*}\"",
        RegexOptions.Compiled);

    public static IReadOnlyList<string> ParseProjectPaths(string solutionPath)
    {
        string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;
        string extension = Path.GetExtension(solutionPath);

        IEnumerable<string> relativePaths = string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase)
            ? ParseSlnx(solutionPath)
            : ParseClassicSln(solutionPath);

        return relativePaths
            .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFullPath(Path.Combine(solutionDirectory, path.Replace('\\', '/'))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> ParseSlnx(string solutionPath)
    {
        XDocument document = XDocument.Load(solutionPath);

        return document.Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!);
    }

    private static IEnumerable<string> ParseClassicSln(string solutionPath)
    {
        List<string> paths = new();
        bool hasHeader = false;

        foreach (string line in File.ReadLines(solutionPath))
        {
            if (!hasHeader && line.TrimStart()
                    .StartsWith("Microsoft Visual Studio Solution File", StringComparison.OrdinalIgnoreCase))
            {
                hasHeader = true;
            }

            Match match = _classicSlnProjectLine.Match(line);
            if (match.Success)
            {
                paths.Add(match.Groups[1].Value);
            }
        }

        if (!hasHeader)
        {
            throw new FormatException(
                "File does not contain the expected 'Microsoft Visual Studio Solution File' header.");
        }

        return paths;
    }
}
