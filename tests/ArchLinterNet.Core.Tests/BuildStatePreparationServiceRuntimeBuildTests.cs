using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class BuildStatePreparationServiceRuntimeBuildTests
{
    [Test]
    public void WriteTemporaryRuntimeGraphBuildProject_WritesGeneratedDriverToUniqueTemporaryProject()
    {
        ArchitectureDiscoveredProject project = new("src/App/App.csproj", "App", ["net10.0"]);
        BuildStatePreflightRequest request = CreateRequest(Path.GetTempPath(), [project], ["App"]);

        string path = BuildStatePreparationService.WriteTemporaryRuntimeGraphBuildProject(request);
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path), Is.True);
                Assert.That(Path.GetExtension(path), Is.EqualTo(".proj"));
                Assert.That(File.ReadAllText(path), Does.Contain("<BuildStateProject Include="));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void CreateRuntimeGraphBuildProjectContent_BuildsOnlyGraphRootsAndForwardsAllOutputContext()
    {
        const string AppPath = "src/App & Host/App.csproj";
        const string LibraryPath = "src/Library/Library.csproj";
        ArchitectureDiscoveredProject library = new(LibraryPath, "Library", ["net10.0"]);
        ArchitectureDiscoveredProject app = new(AppPath, "App", ["net10.0"])
        {
            ProjectReferences = [new ArchitectureDiscoveredProjectReference(LibraryPath, AppPath)],
        };
        string repositoryRoot = Path.Combine(Path.GetTempPath(), "archlinternet-runtime & build");

        string content = BuildStatePreparationService.CreateRuntimeGraphBuildProjectContent(CreateRequest(
            repositoryRoot, [app, library], ["App"], configuration: "Release", targetFramework: "net10.0",
            platform: "AnyCPU", runtimeIdentifier: "win-x64"));

        string appAbsolutePath = Path.GetFullPath(Path.Combine(repositoryRoot, AppPath));
        string libraryAbsolutePath = Path.GetFullPath(Path.Combine(repositoryRoot, LibraryPath));
        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain($"<BuildStateProject Include=\"{System.Security.SecurityElement.Escape(appAbsolutePath)}\" />"));
            Assert.That(content, Does.Not.Contain(libraryAbsolutePath));
            Assert.That(content, Does.Contain("Targets=\"Restore\" BuildInParallel=\"false\" Properties=\"RestoreDisableParallel=true;Configuration=Release;TargetFramework=net10.0;Platform=AnyCPU;RuntimeIdentifier=win-x64\""));
            Assert.That(content, Does.Contain("Targets=\"Build\" BuildInParallel=\"false\" Properties=\"RestoreDisableParallel=true;Configuration=Release;TargetFramework=net10.0;Platform=AnyCPU;RuntimeIdentifier=win-x64;Restore=false\""));
        });
    }

    [Test]
    public void CreateRuntimeGraphBuildProjectContent_CyclicGraphIncludesEveryProjectAndUnconstrainedProperties()
    {
        const string FirstPath = "src/First/First.csproj";
        const string SecondPath = "src/Second/Second.csproj";
        ArchitectureDiscoveredProject first = new(FirstPath, "First", ["net10.0"])
        {
            ProjectReferences = [new ArchitectureDiscoveredProjectReference(SecondPath, FirstPath)],
        };
        ArchitectureDiscoveredProject second = new(SecondPath, "Second", ["net10.0"])
        {
            ProjectReferences = [new ArchitectureDiscoveredProjectReference(FirstPath, SecondPath)],
        };
        string repositoryRoot = Path.Combine(Path.GetTempPath(), "archlinternet-runtime-cycle");

        string content = BuildStatePreparationService.CreateRuntimeGraphBuildProjectContent(CreateRequest(
            repositoryRoot, [first, second], Array.Empty<string>()));

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain(Path.GetFullPath(Path.Combine(repositoryRoot, FirstPath))));
            Assert.That(content, Does.Contain(Path.GetFullPath(Path.Combine(repositoryRoot, SecondPath))));
            Assert.That(content, Does.Contain("Properties=\"RestoreDisableParallel=true\""));
            Assert.That(content, Does.Contain("Properties=\"RestoreDisableParallel=true;Restore=false\""));
        });
    }

    private static BuildStatePreflightRequest CreateRequest(
        string repositoryRoot,
        IReadOnlyCollection<ArchitectureDiscoveredProject> projects,
        IReadOnlyCollection<string> missingAssemblyNames,
        string? configuration = null,
        string? targetFramework = null,
        string? platform = null,
        string? runtimeIdentifier = null)
    {
        ProjectDiscoveryResult discovery = new(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = projects,
        };
        return new BuildStatePreflightRequest(
            repositoryRoot,
            discovery,
            new BuildStateResolvedAssemblies(Array.Empty<System.Reflection.Assembly>(), missingAssemblyNames),
            BuildPreparationMode.EnsureBuilt,
            RequestedConfiguration: configuration,
            RequestedTargetFramework: targetFramework,
            RequestedPlatform: platform,
            RequestedRuntimeIdentifier: runtimeIdentifier);
    }
}
