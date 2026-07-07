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

        List<ArchitectureDiscoveredPackageReference> packageReferences =
            ParsePackageReferences(document, projectPath, fileSystem);
        Dictionary<string, ArchitectureDiscoveredProjectProperty> properties =
            ParseProperties(document, projectPath, fileSystem);
        List<ArchitectureDiscoveredFriendAssembly> friendAssemblies = ParseFriendAssemblies(document, projectPath);
        List<ArchitectureDiscoveredProjectReference> projectReferences = ParseProjectReferences(document, projectPath);

        return new DiscoveredProjectFile(
            Path.GetFullPath(projectPath),
            resolvedAssemblyName,
            frameworks,
            packageReferences,
            properties,
            friendAssemblies,
            projectReferences);
    }

    private static Dictionary<string, ArchitectureDiscoveredProjectProperty> ParseProperties(
        XDocument document, string projectPath, IArchitectureFileSystem fileSystem)
    {
        Dictionary<string, ArchitectureDiscoveredProjectProperty> properties =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string propsPath in EnumerateDirectoryBuildProps(projectPath, fileSystem))
        {
            XDocument propsDocument = XDocument.Parse(fileSystem.ReadAllText(propsPath));
            MergeScalarProperties(propsDocument, propsPath, properties);
        }

        MergeScalarProperties(document, Path.GetFullPath(projectPath), properties);
        return properties;
    }

    private static List<ArchitectureDiscoveredPackageReference> ParsePackageReferences(
        XDocument document, string projectPath, IArchitectureFileSystem fileSystem)
    {
        List<(string PackageId, string? Version)> rawReferences = document.Descendants("PackageReference")
            .Select(element =>
            {
                string? id = element.Attribute("Include")?.Value;
                string? version = element.Attribute("Version")?.Value ?? element.Element("Version")?.Value;
                return (Id: id, Version: string.IsNullOrWhiteSpace(version) ? null : version.Trim());
            })
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Id))
            .Select(reference => (PackageId: reference.Id!.Trim(), reference.Version))
            .ToList();

        if (rawReferences.Count == 0)
        {
            return new List<ArchitectureDiscoveredPackageReference>();
        }

        Dictionary<string, string>? centralPackageVersions = null;

        return rawReferences
            .Select(reference =>
            {
                if (reference.Version != null)
                {
                    return new ArchitectureDiscoveredPackageReference(reference.PackageId, reference.Version);
                }

                centralPackageVersions ??= LoadCentralPackageVersions(projectPath, fileSystem);
                string? resolvedVersion = centralPackageVersions.TryGetValue(reference.PackageId, out string? version)
                    ? version
                    : null;

                return new ArchitectureDiscoveredPackageReference(reference.PackageId, resolvedVersion);
            })
            .ToList();
    }

    private static List<ArchitectureDiscoveredFriendAssembly> ParseFriendAssemblies(XDocument document, string projectPath)
    {
        string sourcePath = Path.GetFullPath(projectPath);

        return document.Descendants("InternalsVisibleTo")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => new ArchitectureDiscoveredFriendAssembly(value, sourcePath))
            .ToList();
    }

    private static List<ArchitectureDiscoveredProjectReference> ParseProjectReferences(XDocument document, string projectPath)
    {
        string projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? string.Empty;
        string sourcePath = Path.GetFullPath(projectPath);

        return document.Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFullPath(Path.Combine(projectDirectory, value!.Trim())))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(value => new ArchitectureDiscoveredProjectReference(value, sourcePath))
            .ToList();
    }

    private static IEnumerable<string> EnumerateDirectoryBuildProps(string projectPath, IArchitectureFileSystem fileSystem)
    {
        List<string> propsPaths = new();
        string? directory = Path.GetDirectoryName(Path.GetFullPath(projectPath));

        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(directory, "Directory.Build.props");
            if (fileSystem.FileExists(candidate))
            {
                propsPaths.Add(candidate);
            }

            string? parent = Path.GetDirectoryName(directory);
            if (string.Equals(parent, directory, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            directory = parent;
        }

        propsPaths.Reverse();
        return propsPaths;
    }

    private static void MergeScalarProperties(
        XDocument document,
        string sourcePath,
        IDictionary<string, ArchitectureDiscoveredProjectProperty> properties)
    {
        foreach (XElement element in document.Descendants("PropertyGroup").Elements())
        {
            if (element.HasElements)
            {
                continue;
            }

            string name = element.Name.LocalName;
            string? value = element.Value;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            properties[name] = new ArchitectureDiscoveredProjectProperty(name, value.Trim(), sourcePath);
        }
    }

    private static Dictionary<string, string> LoadCentralPackageVersions(
        string projectPath, IArchitectureFileSystem fileSystem)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(projectPath));

        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(directory, "Directory.Packages.props");
            if (fileSystem.FileExists(candidate))
            {
                XDocument propsDocument = XDocument.Parse(fileSystem.ReadAllText(candidate));
                return propsDocument.Descendants("PackageVersion")
                    .Select(element => (Id: element.Attribute("Include")?.Value, Version: element.Attribute("Version")?.Value))
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Id) && !string.IsNullOrWhiteSpace(entry.Version))
                    .GroupBy(entry => entry.Id!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().Version!.Trim(), StringComparer.OrdinalIgnoreCase);
            }

            string? parent = Path.GetDirectoryName(directory);
            if (string.Equals(parent, directory, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            directory = parent;
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
