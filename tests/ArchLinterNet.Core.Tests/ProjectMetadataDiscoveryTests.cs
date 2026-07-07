using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
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

    [Test]
    public void Discovery_ParsesSourceLevelInternalsVisibleToAndContractFlagsForbiddenFriendAssembly()
    {
        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(Path.Combine(projectDir, "Properties"));
        File.WriteAllText(Path.Combine(projectDir, "Properties", "AssemblyInfo.cs"), """
            using System.Runtime.CompilerServices;

            [assembly: InternalsVisibleTo("MyApp.Tools")]
            """);
        File.WriteAllText(Path.Combine(projectDir, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        ArchitectureProjectMetadataContract contract = new()
        {
            Id = "friend-assemblies",
            Name = "friend-assemblies",
            Projects = new List<string> { "src/MyApp/MyApp.csproj" },
            AllowedFriendAssemblies = new List<string> { "MyApp.Tests" }
        };
        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyApp.csproj") }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureProjectMetadataContract> { contract }
            }
        };

        ProjectDiscoveryResult discovery = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false);
        ArchitectureDiscoveredProject project = discovery.DiscoveredProjects.Single();
        ArchitectureAnalysisContext context = new(
            _repoRoot,
            new[] { typeof(ProjectMetadataDiscoveryTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
        ArchitectureContractRunner runner = new(context, document);

        List<ArchitectureViolation> violations = runner.Session.CheckProjectMetadataContract(contract);

        Assert.That(project.FriendAssemblies.Select(entry => entry.AssemblyName), Is.EqualTo(new[] { "MyApp.Tools" }));
        Assert.That(project.FriendAssemblies.Single().SourcePath, Is.EqualTo("src/MyApp/Properties/AssemblyInfo.cs"));
        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ProjectMetadataKind, Is.EqualTo("friend_assembly"));
        Assert.That(violations[0].ProjectMetadataActualValue, Is.EqualTo("MyApp.Tools"));
        Assert.That(violations[0].ProjectMetadataSourcePath, Is.EqualTo("src/MyApp/Properties/AssemblyInfo.cs"));
    }
}
