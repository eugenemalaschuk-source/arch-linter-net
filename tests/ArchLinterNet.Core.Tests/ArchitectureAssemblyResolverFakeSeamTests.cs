using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureAssemblyResolverFakeSeamTests
{
    [Test]
    public void ResolveFromDocument_AssemblyAlreadyLoaded_UsesLoaderSeamWithoutLoadingFromDisk()
    {
        var thisAssembly = typeof(ArchitectureAssemblyResolverFakeSeamTests).Assembly;
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { thisAssembly.GetName().Name! }
            }
        };

        var fakeAssemblyLoader = new FakeArchitectureAssemblyLoader(new[] { thisAssembly });

        ResolutionResult result = ArchitectureAssemblyResolver.ResolveFromDocument(
            document,
            repositoryRoot: null,
            fileSystem: new FakeArchitectureFileSystem(),
            environment: new FakeArchitectureEnvironment(),
            assemblyLoader: fakeAssemblyLoader);

        Assert.That(result.ResolvedAssemblies, Has.Member(thisAssembly));
        Assert.That(result.MissingAssemblyNames, Is.Empty);
        Assert.That(fakeAssemblyLoader.LoadWasCalled, Is.False);
        Assert.That(fakeAssemblyLoader.LoadFromWasCalled, Is.False);
    }

    [Test]
    public void ResolveFromDocument_EnvironmentProbingPath_UsesFakeEnvironmentVariable()
    {
        string probingPath = "/fake/probing/path";
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "MissingAssembly" }
            }
        };

        var fileSystem = new FakeArchitectureFileSystem();
        fileSystem.AddDirectory(probingPath);

        var environment = new FakeArchitectureEnvironment();
        environment.SetEnvironmentVariable("ARCHITECTURE_ASSEMBLY_SEARCH_PATHS", probingPath);

        ResolutionResult result = ArchitectureAssemblyResolver.ResolveFromDocument(
            document,
            repositoryRoot: null,
            fileSystem: fileSystem,
            environment: environment,
            assemblyLoader: new FakeArchitectureAssemblyLoader(Array.Empty<System.Reflection.Assembly>()));

        Assert.That(result.AssemblyProbingPaths, Has.Member(probingPath));
        Assert.That(result.MissingAssemblyNames, Has.Member("MissingAssembly"));
    }
}
