using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ProjectMetadataDiscoveryTests
{
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-project-metadata-{Guid.NewGuid():N}");
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

    [Test]
    public void Discovery_ParsesInheritedPropertiesFriendAssembliesAndProjectReferences()
    {
        File.WriteAllText(Path.Combine(_repoRoot, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
            </Project>
            """);

        string testsDir = Path.Combine(_repoRoot, "tests", "MyApp.Tests");
        Directory.CreateDirectory(testsDir);
        File.WriteAllText(Path.Combine(testsDir, "MyApp.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <InternalsVisibleTo Include="MyApp.Tests" />
                <ProjectReference Include="../../tests/MyApp.Tests/MyApp.Tests.csproj" />
              </ItemGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyApp.csproj") }
            }
        };

        ArchitectureDiscoveredProject project = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false)
            .DiscoveredProjects
            .Single();

        Assert.That(project.Properties["Nullable"].Value, Is.EqualTo("enable"));
        Assert.That(project.Properties["TreatWarningsAsErrors"].Value, Is.EqualTo("true"));
        Assert.That(project.Properties["TreatWarningsAsErrors"].SourcePath, Is.EqualTo("Directory.Build.props"));
        Assert.That(project.FriendAssemblies.Select(entry => entry.AssemblyName), Is.EqualTo(new[] { "MyApp.Tests" }));
        Assert.That(project.ProjectReferences.Select(entry => entry.Path), Is.EqualTo(new[] { "tests/MyApp.Tests/MyApp.Tests.csproj" }));
    }
}
