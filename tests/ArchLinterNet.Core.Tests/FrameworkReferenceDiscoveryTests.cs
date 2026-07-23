using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class FrameworkReferenceDiscoveryTests
{
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-framework-discovery-{Guid.NewGuid():N}");
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
    public void FrameworkReference_Include_IsParsed()
    {
        string projectDir = CreateProject(
            "Sample", """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.FrameworkReferences, Has.Count.EqualTo(1));
        Assert.That(project.FrameworkReferences[0].FrameworkName, Is.EqualTo("Microsoft.AspNetCore.App"));
        Assert.That(project.FrameworkReferences[0].Condition, Is.Null);
    }

    [Test]
    public void FrameworkReference_Condition_IsParsed()
    {
        string projectDir = CreateProject(
            "Sample",
            """<FrameworkReference Include="Microsoft.AspNetCore.App" Condition="'$(TargetFramework)'=='net10.0'" />""");

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.FrameworkReferences, Has.Count.EqualTo(1));
        Assert.That(project.FrameworkReferences[0].FrameworkName, Is.EqualTo("Microsoft.AspNetCore.App"));
        Assert.That(project.FrameworkReferences[0].Condition, Is.EqualTo("'$(TargetFramework)'=='net10.0'"));
    }

    [Test]
    public void FrameworkReference_NoFrameworkReferences_ReturnsEmptyList()
    {
        string projectDir = CreateProject("Sample", string.Empty);

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.FrameworkReferences, Is.Empty);
    }

    [Test]
    public void FrameworkReference_MultipleProjects_EachDiscoveredIndependently()
    {
        string apiDir = CreateProject("Api", """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
        string workerDir = CreateProject("Worker", string.Empty);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string>
                {
                    Path.Combine(apiDir, "Api.csproj"),
                    Path.Combine(workerDir, "Worker.csproj"),
                }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false);

        ArchitectureDiscoveredProject api = result.DiscoveredProjects.Single(p => p.AssemblyName == "Api");
        ArchitectureDiscoveredProject worker = result.DiscoveredProjects.Single(p => p.AssemblyName == "Worker");

        Assert.That(api.FrameworkReferences, Has.Count.EqualTo(1));
        Assert.That(api.FrameworkReferences[0].FrameworkName, Is.EqualTo("Microsoft.AspNetCore.App"));
        Assert.That(worker.FrameworkReferences, Is.Empty);
    }

    [Test]
    public void FrameworkReference_MultipleConditionalReferencesInSameProject_AreBothDiscovered()
    {
        string projectDir = CreateProject(
            "Sample",
            """
            <FrameworkReference Include="Microsoft.AspNetCore.App" Condition="'$(TargetFramework)'=='net10.0'" />
            <FrameworkReference Include="Microsoft.AspNetCore.App" Condition="'$(TargetFramework)'=='net472'" />
            """);

        ArchitectureDiscoveredProject project = DiscoverSingleProject(Path.Combine(projectDir, "Sample.csproj"));

        Assert.That(project.FrameworkReferences, Has.Count.EqualTo(2));
        Assert.That(project.FrameworkReferences.Select(f => f.Condition),
            Is.EquivalentTo(new[] { "'$(TargetFramework)'=='net10.0'", "'$(TargetFramework)'=='net472'" }));
    }
}
