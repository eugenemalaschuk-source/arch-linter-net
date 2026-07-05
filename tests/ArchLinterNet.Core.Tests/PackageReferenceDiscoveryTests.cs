using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PackageReferenceDiscoveryTests
{
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-package-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, true);
        }
    }

    private string CreateProject(string assemblyName, string projectBody, string? subdirectory = null)
    {
        string projectDir = Path.Combine(_repoRoot, subdirectory ?? assemblyName);
        Directory.CreateDirectory(projectDir);

        File.WriteAllText(Path.Combine(projectDir, $"{assemblyName}.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                {projectBody}
              </ItemGroup>
            </Project>
            """);

        return projectDir;
    }

    private ArchitectureDiscoveredProject DiscoverSingleProject(string projectCsprojPath)
    {
        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { projectCsprojPath }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false);

        return result.DiscoveredProjects.Single();
    }

    [Test]
    public void PackageReference_AttributeVersion_IsParsed()
    {
        string projectDir = CreateProject(
            "Sample", """<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />""");

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.PackageReferences, Has.Count.EqualTo(1));
        Assert.That(project.PackageReferences[0].PackageId, Is.EqualTo("Newtonsoft.Json"));
        Assert.That(project.PackageReferences[0].Version, Is.EqualTo("13.0.3"));
    }

    [Test]
    public void PackageReference_ChildElementVersion_IsParsed()
    {
        string projectDir = CreateProject(
            "Sample", """
            <PackageReference Include="Newtonsoft.Json">
                <Version>13.0.3</Version>
            </PackageReference>
            """);

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.PackageReferences, Has.Count.EqualTo(1));
        Assert.That(project.PackageReferences[0].PackageId, Is.EqualTo("Newtonsoft.Json"));
        Assert.That(project.PackageReferences[0].Version, Is.EqualTo("13.0.3"));
    }

    [Test]
    public void PackageReference_NoVersionAndNoCentralPackageManagement_VersionIsNull()
    {
        string projectDir = CreateProject(
            "Sample", """<PackageReference Include="Newtonsoft.Json" />""");

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.PackageReferences, Has.Count.EqualTo(1));
        Assert.That(project.PackageReferences[0].PackageId, Is.EqualTo("Newtonsoft.Json"));
        Assert.That(project.PackageReferences[0].Version, Is.Null);
    }

    [Test]
    public void PackageReference_NoPackageReferences_ReturnsEmptyList()
    {
        string projectDir = CreateProject("Sample", string.Empty);

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.PackageReferences, Is.Empty);
    }

    [Test]
    public void PackageReference_VersionResolvedFromCentralPackageManagement()
    {
        string projectDir = CreateProject(
            "Sample", """<PackageReference Include="Newtonsoft.Json" />""");
        File.WriteAllText(Path.Combine(_repoRoot, "Directory.Packages.props"), """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.PackageReferences[0].Version, Is.EqualTo("13.0.3"));
    }

    [Test]
    public void PackageReference_NoMatchingCentralPackageVersion_VersionRemainsNull()
    {
        string projectDir = CreateProject(
            "Sample", """<PackageReference Include="Newtonsoft.Json" />""");
        File.WriteAllText(Path.Combine(_repoRoot, "Directory.Packages.props"), """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Some.Other.Package" Version="1.2.3" />
              </ItemGroup>
            </Project>
            """);

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.PackageReferences[0].Version, Is.Null);
    }

    [Test]
    public void PackageReference_ExplicitVersionIsNotOverriddenByCentralPackageManagement()
    {
        string projectDir = CreateProject(
            "Sample", """<PackageReference Include="Newtonsoft.Json" Version="12.0.0" />""");
        File.WriteAllText(Path.Combine(_repoRoot, "Directory.Packages.props"), """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.PackageReferences[0].Version, Is.EqualTo("12.0.0"));
    }

    [Test]
    public void PackageReference_NearestDirectoryPackagesPropsWins()
    {
        string nestedRoot = Path.Combine(_repoRoot, "nested");
        Directory.CreateDirectory(nestedRoot);
        string projectDir = Path.Combine(nestedRoot, "Sample");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" />
              </ItemGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(_repoRoot, "Directory.Packages.props"), """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="11.0.0" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(nestedRoot, "Directory.Packages.props"), """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.PackageReferences[0].Version, Is.EqualTo("13.0.3"));
    }

    [Test]
    public void PackageReference_AvailableEvenWithoutResolvedBuildOutput()
    {
        // Discovery is called with resolveAssemblyOutputs: false above for every test in this file;
        // this test asserts that behavior explicitly as its own scenario.
        string projectDir = CreateProject(
            "Sample", """<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />""");

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "Sample.csproj") }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: true);

        Assert.That(result.Diagnostics, Has.Some.Matches<ArchitectureProjectDiscoveryDiagnostic>(
            d => d.Kind == "missing project build output"));
        Assert.That(result.DiscoveredProjects.Single().PackageReferences, Has.Count.EqualTo(1));
    }
}
