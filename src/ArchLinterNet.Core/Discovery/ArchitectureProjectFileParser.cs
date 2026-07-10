using System.Xml.Linq;
using ArchLinterNet.Core.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchLinterNet.Core.Discovery;

internal interface IArchitectureProjectFileParser
{
    DiscoveredProjectFile Parse(string projectPath, IArchitectureFileSystem? fileSystem = null);
}

internal sealed class ArchitectureProjectFileParser : IArchitectureProjectFileParser
{
    private const string IncludeAttribute = "Include";

    public DiscoveredProjectFile Parse(string projectPath, IArchitectureFileSystem? fileSystem = null)
    {
        fileSystem ??= ArchitectureFileSystem.Real;
        XDocument document = XDocument.Parse(fileSystem.ReadAllText(projectPath));

        IEnumerable<XElement> propertyGroups = document.Descendants("PropertyGroup");

        string resolvedAssemblyName = ResolveAssemblyName(propertyGroups, projectPath);
        List<string> frameworks = ResolveTargetFrameworks(propertyGroups);

        List<ArchitectureDiscoveredPackageReference> packageReferences =
            ParsePackageReferences(document, projectPath, fileSystem);
        Dictionary<string, ArchitectureDiscoveredProjectProperty> properties =
            ParseProperties(document, projectPath, fileSystem);
        List<ArchitectureDiscoveredFriendAssembly> friendAssemblies = ParseFriendAssemblies(document, projectPath, fileSystem);
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

    private static string ResolveAssemblyName(IEnumerable<XElement> propertyGroups, string projectPath)
    {
        string? assemblyName = propertyGroups
            .Select(group => group.Element("AssemblyName")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return string.IsNullOrWhiteSpace(assemblyName)
            ? Path.GetFileNameWithoutExtension(projectPath)
            : assemblyName.Trim();
    }

    private static List<string> ResolveTargetFrameworks(IEnumerable<XElement> propertyGroups)
    {
        string? targetFrameworks = propertyGroups
            .Select(group => group.Element("TargetFrameworks")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(targetFrameworks))
        {
            return targetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        string? targetFramework = propertyGroups
            .Select(group => group.Element("TargetFramework")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return !string.IsNullOrWhiteSpace(targetFramework)
            ? new List<string> { targetFramework.Trim() }
            : new List<string>();
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
                string? id = element.Attribute(IncludeAttribute)?.Value;
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

    private static List<ArchitectureDiscoveredFriendAssembly> ParseFriendAssemblies(
        XDocument document, string projectPath, IArchitectureFileSystem fileSystem)
    {
        string sourcePath = Path.GetFullPath(projectPath);
        IEnumerable<ArchitectureDiscoveredFriendAssembly> projectFileDeclarations = document.Descendants("InternalsVisibleTo")
            .Select(element => element.Attribute(IncludeAttribute)?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => new ArchitectureDiscoveredFriendAssembly(value!.Trim(), sourcePath));

        return projectFileDeclarations
            .Concat(ParseSourceFriendAssemblies(projectPath, fileSystem))
            .GroupBy(entry => entry.AssemblyName, StringComparer.Ordinal)
            .Select(group => group.OrderBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase).First())
            .OrderBy(entry => entry.AssemblyName, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<ArchitectureDiscoveredFriendAssembly> ParseSourceFriendAssemblies(
        string projectPath, IArchitectureFileSystem fileSystem)
    {
        string projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? string.Empty;
        if (!fileSystem.DirectoryExists(projectDirectory))
        {
            yield break;
        }

        foreach (string file in fileSystem.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                     .Where(file => !IsUnderBuildOutputDirectory(projectDirectory, file))
                     .OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
        {
            string sourceText = fileSystem.ReadAllText(file);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

            foreach (string assemblyName in CollectFriendAssembliesFromAttributes(root.AttributeLists).OfType<string>())
            {
                yield return new ArchitectureDiscoveredFriendAssembly(assemblyName, Path.GetFullPath(file));
            }

            foreach (MemberDeclarationSyntax member in root.Members)
            {
                foreach (string assemblyName in CollectFriendAssembliesFromAttributes(member.AttributeLists).OfType<string>())
                {
                    yield return new ArchitectureDiscoveredFriendAssembly(assemblyName, Path.GetFullPath(file));
                }
            }
        }
    }

    private static IEnumerable<string?> CollectFriendAssembliesFromAttributes(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (AttributeListSyntax attributeList in attributeLists)
        {
            if (attributeList.Target?.Identifier.IsKind(SyntaxKind.AssemblyKeyword) != true)
            {
                continue;
            }

            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                yield return ExtractInternalsVisibleToAssemblyName(attribute);
            }
        }
    }

    private static string? ExtractInternalsVisibleToAssemblyName(AttributeSyntax attribute)
    {
        string nameText = attribute.Name.ToString();
        if (!nameText.EndsWith("InternalsVisibleTo", StringComparison.Ordinal)
            && !nameText.EndsWith("InternalsVisibleToAttribute", StringComparison.Ordinal))
        {
            return null;
        }

        SeparatedSyntaxList<AttributeArgumentSyntax>? arguments = attribute.ArgumentList?.Arguments;
        if (arguments == null || arguments.Value.Count != 1)
        {
            return null;
        }

        ExpressionSyntax expression = arguments.Value[0].Expression;
        if (expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText.Trim();
        }

        return null;
    }

    private static List<ArchitectureDiscoveredProjectReference> ParseProjectReferences(XDocument document, string projectPath)
    {
        string projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? string.Empty;
        string sourcePath = Path.GetFullPath(projectPath);

        return document.Descendants("ProjectReference")
            .Select(element => element.Attribute(IncludeAttribute)?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeProjectReferencePath(projectDirectory, value!.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(value => new ArchitectureDiscoveredProjectReference(value, sourcePath))
            .ToList();
    }

    private static string NormalizeProjectReferencePath(string projectDirectory, string include)
    {
        char separator = Path.DirectorySeparatorChar;
        string normalized = include
            .Replace('\\', separator)
            .Replace('/', separator);
        return Path.GetFullPath(Path.Combine(projectDirectory, normalized));
    }

    private static IEnumerable<string> EnumerateDirectoryBuildProps(string projectPath, IArchitectureFileSystem fileSystem)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(projectPath));

        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(directory, "Directory.Build.props");
            if (fileSystem.FileExists(candidate))
            {
                yield return candidate;
                yield break;
            }

            string? parent = Path.GetDirectoryName(directory);
            if (string.Equals(parent, directory, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            directory = parent;
        }
    }

    private static bool IsUnderBuildOutputDirectory(string projectDirectory, string filePath)
    {
        string relativePath = Path.GetRelativePath(projectDirectory, filePath);
        string[] segments = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);

        return segments.Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
    }

    private static void MergeScalarProperties(
        XDocument document,
        string sourcePath,
        Dictionary<string, ArchitectureDiscoveredProjectProperty> properties)
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
                    .Select(element => (Id: element.Attribute(IncludeAttribute)?.Value, Version: element.Attribute("Version")?.Value))
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
