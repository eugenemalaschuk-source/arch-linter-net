using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class DiscoveredProjectSourceSetTests
{
    private static readonly string[] _value = { "src/Production/Production.csproj" };
    private static readonly string[] _value1 = { "src/Production/Production.csproj" };
    private string _repositoryRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-discovered-project-sets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repositoryRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repositoryRoot))
        {
            Directory.Delete(_repositoryRoot, recursive: true);
        }
    }

    [Test]
    public void ProjectSets_BindToFilteredSolutionInventoryThroughPathGlob()
    {
        WriteProject("src/Production/Production.csproj");
        WriteProject("tests/Production.Tests/Production.Tests.csproj");
        File.WriteAllText(Path.Combine(_repositoryRoot, "Fixture.slnx"), """
            <Solution>
              <Project Path="src/Production/Production.csproj" />
              <Project Path="tests/Production.Tests/Production.Tests.csproj" />
            </Solution>
            """);
        string policyPath = Path.Combine(_repositoryRoot, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Discovered project sets
            analysis:
              solution: Fixture.slnx
              project_exclude: ["tests/**"]
            source_sets:
              production_projects:
                kind: project
                globs: ["src/**/*.csproj"]
            contracts:
              strict_project_metadata:
                - name: production projects require nullable
                  id: production-nullable
                  project_sets: [production_projects]
                  required_properties:
                    Nullable: enable
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        ProjectDiscoveryResult discovery = new ArchitectureProjectDiscoveryService()
            .ResolveAndApply(document, _repositoryRoot, resolveAssemblyOutputs: false);

        ArchitectureSourceSetExpander.BindProjectSets(document, discovery);
        ArchitectureSourceSetExpander.BindProjectSets(document, discovery);

        ArchitectureProjectMetadataContract contract = document.Contracts.StrictProjectMetadata.Single();
        Assert.Multiple(() =>
        {
            Assert.That(contract.Projects, Is.EqualTo(_value));
            Assert.That(document.SourceExpansion.Sets.Single(set => set.Name == "production_projects").ResolvedSources,
                Is.EqualTo(_value1));
            Assert.That(document.SourceExpansion.Contracts.Single().Instances.Single().Selector,
                Is.EqualTo("src/**/*.csproj"));
        });
    }

    [Test]
    public void ProjectSetMember_IsRejectedWhenSolutionFilteringLeavesNoProjects()
    {
        WriteProject("src/Production/Production.csproj");
        File.WriteAllText(Path.Combine(_repositoryRoot, "Fixture.slnx"), "<Solution><Project Path=\"src/Production/Production.csproj\" /></Solution>");
        string policyPath = Path.Combine(_repositoryRoot, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Empty filtered solution
            analysis:
              solution: Fixture.slnx
              project_exclude: ["src/**"]
            source_sets:
              production_projects:
                kind: project
                members: [src/Production/Production.csproj]
            contracts:
              strict_project_metadata:
                - name: production nullable
                  project_sets: [production_projects]
                  required_properties: { Nullable: enable }
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        ProjectDiscoveryResult discovery = new ArchitectureProjectDiscoveryService().ResolveAndApply(document, _repositoryRoot, false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureSourceSetExpander.BindProjectSets(document, discovery))!;
        Assert.That(exception.Message, Does.Contain("must not introduce sources"));
    }

    [Test]
    public void OptionalProjectGlob_IsRecordedEmptyWhenSolutionFilteringLeavesNoProjects()
    {
        WriteProject("src/Production/Production.csproj");
        File.WriteAllText(Path.Combine(_repositoryRoot, "Fixture.slnx"), "<Solution><Project Path=\"src/Production/Production.csproj\" /></Solution>");
        string policyPath = Path.Combine(_repositoryRoot, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Optional empty filtered solution
            analysis:
              solution: Fixture.slnx
              project_exclude: ["src/**"]
            source_sets:
              future_projects:
                kind: project
                globs: ["src/**/*.csproj"]
                optional: true
                reason: Production projects are introduced later.
            contracts:
              strict_project_metadata:
                - name: future projects nullable
                  project_sets: [future_projects]
                  required_properties: { Nullable: enable }
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        ProjectDiscoveryResult discovery = new ArchitectureProjectDiscoveryService().ResolveAndApply(document, _repositoryRoot, false);

        ArchitectureSourceSetExpander.BindProjectSets(document, discovery);

        Assert.That(document.SourceExpansion.Sets.Single().ResolvedSources, Is.Empty);
        Assert.That(document.SourceExpansion.Contracts.Single().OptionalEmpty, Is.True);
    }

    private void WriteProject(string relativePath)
    {
        string projectPath = Path.Combine(_repositoryRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
    }
}
