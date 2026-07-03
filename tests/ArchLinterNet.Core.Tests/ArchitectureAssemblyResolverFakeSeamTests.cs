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

        var service = new ArchitectureAssemblyResolutionService(
            new FakeArchitectureFileSystem(),
            new FakeArchitectureEnvironment(),
            fakeAssemblyLoader);

        ResolutionResult result = service.ResolveFromDocument(document, repositoryRoot: null);

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

        var service = new ArchitectureAssemblyResolutionService(
            fileSystem,
            environment,
            new FakeArchitectureAssemblyLoader(Array.Empty<System.Reflection.Assembly>()));

        ResolutionResult result = service.ResolveFromDocument(document, repositoryRoot: null);

        Assert.That(result.AssemblyProbingPaths, Has.Member(probingPath));
        Assert.That(result.MissingAssemblyNames, Has.Member("MissingAssembly"));
    }
}
