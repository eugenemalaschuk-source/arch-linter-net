using System.Xml.Linq;
using ArchLinterNet.Core.IO;

namespace ArchLinterNet.Core.Discovery;

internal interface IArchitectureProjectFileParser
{
    DiscoveredProjectFile Parse(string projectPath, IArchitectureFileSystem? fileSystem = null);
}

internal sealed class ArchitectureProjectFileParser : IArchitectureProjectFileParser
{
    public DiscoveredProjectFile Parse(string projectPath, IArchitectureFileSystem? fileSystem = null)
    {
        fileSystem ??= ArchitectureFileSystem.Real;
        XDocument document = XDocument.Parse(fileSystem.ReadAllText(projectPath));

        IEnumerable<XElement> propertyGroups = document.Descendants("PropertyGroup");

        string? assemblyName = propertyGroups
            .Select(group => group.Element("AssemblyName")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        string resolvedAssemblyName = string.IsNullOrWhiteSpace(assemblyName)
            ? Path.GetFileNameWithoutExtension(projectPath)
            : assemblyName.Trim();

        string? targetFrameworks = propertyGroups
            .Select(group => group.Element("TargetFrameworks")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        string? targetFramework = propertyGroups
            .Select(group => group.Element("TargetFramework")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        List<string> frameworks = !string.IsNullOrWhiteSpace(targetFrameworks)
            ? targetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList()
            : !string.IsNullOrWhiteSpace(targetFramework)
                ? new List<string> { targetFramework.Trim() }
                : new List<string>();

        return new DiscoveredProjectFile(Path.GetFullPath(projectPath), resolvedAssemblyName, frameworks);
    }
}
