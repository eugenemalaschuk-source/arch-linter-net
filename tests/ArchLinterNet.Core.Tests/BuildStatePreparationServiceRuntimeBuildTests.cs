using System.Diagnostics;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
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

        string path = BuildStateRuntimeGraphBuildProjectFactory.WriteTemporaryRuntimeGraphBuildProject(request);
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

        string content = BuildStateRuntimeGraphBuildProjectFactory.CreateRuntimeGraphBuildProjectContent(CreateRequest(
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

        string content = BuildStateRuntimeGraphBuildProjectFactory.CreateRuntimeGraphBuildProjectContent(CreateRequest(
            repositoryRoot, [first, second], Array.Empty<string>()));

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain(Path.GetFullPath(Path.Combine(repositoryRoot, FirstPath))));
            Assert.That(content, Does.Contain(Path.GetFullPath(Path.Combine(repositoryRoot, SecondPath))));
            Assert.That(content, Does.Contain("Properties=\"RestoreDisableParallel=true\""));
            Assert.That(content, Does.Contain("Properties=\"RestoreDisableParallel=true;Restore=false\""));
        });
    }

    [Test]
    public void CreateDotnetProcessStartInfo_PreservesStructuredArgumentsAndDisablesShellExecution()
    {
        string repositoryRoot = Path.Combine(Path.GetTempPath(), "archlinternet process & args");
        BuildStatePreflightRequest request = CreateRequest(repositoryRoot, Array.Empty<ArchitectureDiscoveredProject>(), []);
        string projectPath = Path.Combine(repositoryRoot, "src", "App with spaces", "App.csproj");

        ProcessStartInfo startInfo = BuildStateRuntimeBuildProcessExecutor.CreateDotnetProcessStartInfo(
            request, ["build", projectPath, "--no-restore"]);

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.UseShellExecute, Is.False);
            Assert.That(startInfo.RedirectStandardOutput, Is.True);
            Assert.That(startInfo.RedirectStandardError, Is.True);
            Assert.That(startInfo.ArgumentList, Is.EqualTo(new[] { "build", projectPath, "--no-restore" }));
        });
    }

    [Test]
    public void CreateGraphBuildArguments_StandardAndRuntimeSpecificRequestsPreserveTheirDistinctContexts()
    {
        string repositoryRoot = Path.Combine(Path.GetTempPath(), "archlinternet build args");
        BuildStatePreflightRequest standard = CreateRequest(
            repositoryRoot, Array.Empty<ArchitectureDiscoveredProject>(), [], configuration: "Release",
            targetFramework: "net10.0", platform: "AnyCPU", runtimeIdentifier: null);
        BuildStatePreflightRequest runtimeSpecific = standard with { RequestedRuntimeIdentifier = "win-x64" };
        string targetPath = Path.Combine(repositoryRoot, "App with spaces.slnx");

        List<string> standardArguments = BuildStateRuntimeBuildProcessExecutor.CreateGraphBuildArguments(
            standard, targetPath, buildsRuntimeSpecificOutput: false);
        List<string> runtimeArguments = BuildStateRuntimeBuildProcessExecutor.CreateGraphBuildArguments(
            runtimeSpecific, targetPath, buildsRuntimeSpecificOutput: true);

        Assert.Multiple(() =>
        {
            Assert.That(standardArguments, Is.EqualTo(new[]
            {
                "build", targetPath, "--nologo", "--no-restore", "-m:1", "-c", "Release", "-f", "net10.0", "-p:Platform=AnyCPU",
            }));
            Assert.That(runtimeArguments, Is.EqualTo(new[]
            {
                "build", targetPath, "--nologo", "--no-restore", "-m:1",
            }));
        });
    }

    [Test]
    [NonParallelizable]
    public void CreateDotnetProcessStartInfo_UsesExistingDotnetRootExecutable()
    {
        string root = Path.Combine(Path.GetTempPath(), $"archlinternet-dotnet-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string executable = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        File.WriteAllText(executable, string.Empty);
        string? originalDotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", root);

            ProcessStartInfo startInfo = BuildStateRuntimeBuildProcessExecutor.CreateDotnetProcessStartInfo(
                CreateRequest(root, Array.Empty<ArchitectureDiscoveredProject>(), []), ["--version"]);

            Assert.That(startInfo.FileName, Is.EqualTo(executable));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", originalDotnetRoot);
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void RunDotnetCommand_NonZeroExitCapturesStructuredArgumentsInTypedDiagnostic()
    {
        string repositoryRoot = Path.GetTempPath();
        string argumentWithSpaces = Path.Combine(repositoryRoot, "invalid argument with spaces");

        BuildStatePreflightDiagnostic? diagnostic = BuildStateRuntimeBuildProcessExecutor.RunDotnetCommand(
            CreateRequest(repositoryRoot, Array.Empty<ArchitectureDiscoveredProject>(), []),
            ["not-a-dotnet-command", argumentWithSpaces], "build", BuildStatePreflightState.BuildFailed);

        Assert.That(diagnostic, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic!.State, Is.EqualTo(BuildStatePreflightState.BuildFailed));
            Assert.That(diagnostic.Evidence.BuildCommand, Is.EqualTo($"dotnet not-a-dotnet-command \"{argumentWithSpaces}\""));
            Assert.That(diagnostic.Evidence.Detail, Does.Contain("failed with exit code"));
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
