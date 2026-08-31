using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureProjectDiscoveryTests
{
    [Test]
    public void ResolveFromDocument_DistinctArtifactsWithTheSameOutputNameRetainsBothProjectPaths()
    {
        const string OutputAssemblyName = "Shared";
        string firstProject = CreateProjectWithOutputName("First", OutputAssemblyName);
        string secondProject = CreateProjectWithOutputName("Second", OutputAssemblyName);
        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects =
                [
                    Path.Combine(firstProject, "First.csproj"),
                    Path.Combine(secondProject, "Second.csproj"),
                ],
            },
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot);

        Assert.Multiple(() =>
        {
            Assert.That(result.TargetAssemblyNames, Is.EqualTo(new[] { OutputAssemblyName }));
            Assert.That(result.ResolvedAssemblyPathsByNormalizedProjectPath,
                Does.ContainKey("First/First.csproj").WithValue(Path.Combine(
                    firstProject, "bin", "Debug", "net10.0", "Shared.dll")));
            Assert.That(result.ResolvedAssemblyPathsByNormalizedProjectPath,
                Does.ContainKey("Second/Second.csproj").WithValue(Path.Combine(
                    secondProject, "bin", "Debug", "net10.0", "Shared.dll")));
        });
    }

    private string CreateProjectWithOutputName(string projectName, string outputAssemblyName)
    {
        string projectDirectory = Path.Combine(_repoRoot, projectName);
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, $"{projectName}.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{outputAssemblyName}</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        string outputDirectory = Path.Combine(projectDirectory, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, $"{outputAssemblyName}.dll"), string.Empty);
        return projectDirectory;
    }
}
