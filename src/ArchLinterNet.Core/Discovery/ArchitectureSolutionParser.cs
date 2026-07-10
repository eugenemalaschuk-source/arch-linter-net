using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArchLinterNet.Core.IO;

namespace ArchLinterNet.Core.Discovery;

internal interface IArchitectureSolutionParser
{
    IReadOnlyList<string> ParseProjectPaths(string solutionPath, IArchitectureFileSystem? fileSystem = null);
}

internal sealed partial class ArchitectureSolutionParser : IArchitectureSolutionParser
{
    [GeneratedRegex("^Project\\(\"\\{[^}]*}\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"([^\"]*)\",\\s*\"\\{[^}]*}\"",
        RegexOptions.Compiled)]
    private static partial Regex ClassicSlnProjectLine();

    public IReadOnlyList<string> ParseProjectPaths(string solutionPath, IArchitectureFileSystem? fileSystem = null)
    {
        fileSystem ??= ArchitectureFileSystem.Real;
        string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;
        string extension = Path.GetExtension(solutionPath);

        IEnumerable<string> relativePaths = string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase)
            ? ParseSlnx(solutionPath, fileSystem)
            : ParseClassicSln(solutionPath, fileSystem);

        return relativePaths
            .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFullPath(Path.Combine(solutionDirectory, path.Replace('\\', '/'))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> ParseSlnx(string solutionPath, IArchitectureFileSystem fileSystem)
    {
        XDocument document = XDocument.Parse(fileSystem.ReadAllText(solutionPath));

        return document.Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!);
    }

    private static List<string> ParseClassicSln(string solutionPath, IArchitectureFileSystem fileSystem)
    {
        List<string> paths = new();
        bool hasHeader = false;

        foreach (string line in fileSystem.ReadLines(solutionPath))
        {
            if (!hasHeader && line.TrimStart()
                    .StartsWith("Microsoft Visual Studio Solution File", StringComparison.OrdinalIgnoreCase))
            {
                hasHeader = true;
            }

            Match match = ClassicSlnProjectLine().Match(line);
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
