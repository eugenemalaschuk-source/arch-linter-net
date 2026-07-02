using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureProjectDiscoveryServiceFakeFileSystemTests
{
    // ArchitectureProjectDiscoveryService is the most integration-heavy setup dependency: it parses
    // project files, probes build-output directories, and compares source/output timestamps for
    // staleness. Driving it end to end through a fake IArchitectureFileSystem (rather than faking the
    // whole service, as ArchitectureRunnerSetupServiceFakeDependencyTests does) proves the seam
    // introduced by #139 is real: the service's own discovery/timestamp logic runs correctly without
    // touching the real disk.
    [Test]
    public void ResolveAndApply_FakeFileSystem_DiscoversProjectWithoutTouchingRealDisk()
    {
        string repositoryRoot = "/fake/repo";
        string projectPath = "/fake/repo/ProjA/ProjA.csproj";
        string outputDirectory = "/fake/repo/ProjA/bin/Debug/net9.0";
        string dllPath = "/fake/repo/ProjA/bin/Debug/net9.0/ProjA.dll";

        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>ProjA</AssemblyName>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        fileSystem.AddFile(dllPath, string.Empty, new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { "ProjA/ProjA.csproj" }
            }
        };

        var service = new ArchitectureProjectDiscoveryService(fileSystem);

        ProjectDiscoveryResult result = service.ResolveAndApply(document, repositoryRoot, resolveAssemblyOutputs: true);

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.TargetAssemblyNames, Is.EquivalentTo(new[] { "ProjA" }));
        Assert.That(result.AssemblySearchPaths, Is.EquivalentTo(new[] { outputDirectory }));
        Assert.That(document.Analysis.TargetAssemblies, Is.EquivalentTo(new[] { "ProjA" }));
    }

    [Test]
    public void ResolveAndApply_FakeFileSystem_StaleBuildOutput_ReportsStaleDiagnostic()
    {
        string repositoryRoot = "/fake/repo";
        string projectPath = "/fake/repo/ProjA/ProjA.csproj";
        string dllPath = "/fake/repo/ProjA/bin/Debug/net9.0/ProjA.dll";

        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddFile(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>ProjA</AssemblyName>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """, new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        fileSystem.AddFile(dllPath, string.Empty, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { "ProjA/ProjA.csproj" }
            }
        };

        var service = new ArchitectureProjectDiscoveryService(fileSystem);

        ProjectDiscoveryResult result = service.ResolveAndApply(document, repositoryRoot, resolveAssemblyOutputs: true);

        Assert.That(result.Diagnostics.Select(d => d.Kind), Has.Member("stale project build output"));
        Assert.That(result.TargetAssemblyNames, Is.Empty);
    }
}
