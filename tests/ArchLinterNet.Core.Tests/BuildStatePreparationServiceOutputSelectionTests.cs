using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class BuildStatePreparationServiceOutputSelectionTests
{
    private const string ProjectPath = "fixture/Fixture.csproj";
    private string _repositoryRoot = null!;
    private string _projectDirectory = null!;
    private ArchitectureDiscoveredProject _project = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-output-selection-{Guid.NewGuid():N}");
        _projectDirectory = Path.Combine(_repositoryRoot, "fixture");
        Directory.CreateDirectory(_projectDirectory);
        File.WriteAllText(Path.Combine(_projectDirectory, "Fixture.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        _project = new ArchitectureDiscoveredProject(ProjectPath, "Fixture", ["net10.0", "net11.0"]);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repositoryRoot))
        {
            Directory.Delete(_repositoryRoot, true);
        }
    }

    [Test]
    public void ResolveBuiltAssemblyPath_UnknownProjectDirectoryReturnsNull()
    {
        string debug = WriteOutput("Debug", "net10.0", "Fixture");

        string? resolved = BuildStatePreparationService.ResolveBuiltAssemblyPath(
            CreateRequest(debug), _project, projectDirectory: null);

        Assert.That(resolved, Is.Null);
    }

    [Test]
    public void ResolveBuiltAssemblyPath_UnconstrainedRequestReusesPreparedDebugOutputWhenReleaseIsNewer()
    {
        string debug = WriteOutput("Debug", "net10.0", "Fixture");
        string release = WriteOutput("Release", "net10.0", "Fixture");
        File.SetLastWriteTimeUtc(release, File.GetLastWriteTimeUtc(debug).AddMinutes(1));

        string? resolved = BuildStatePreparationService.ResolveBuiltAssemblyPath(
            CreateRequest(debug), _project, _projectDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(BuildStatePreparationService.IsProjectOutput(_projectDirectory, _project.AssemblyName, debug), Is.True);
            Assert.That(resolved, Is.EqualTo(debug));
        });
    }

    [Test]
    public void ResolveBuiltAssemblyPath_MissingPreparedOutputFallsBackToBuiltOutputSearch()
    {
        string missingDebug = Path.Combine(_projectDirectory, "bin", "Debug", "net10.0", "Fixture.dll");
        string release = WriteOutput("Release", "net10.0", "Fixture");

        string? resolved = BuildStatePreparationService.ResolveBuiltAssemblyPath(
            CreateRequest(missingDebug), _project, _projectDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(BuildStatePreparationService.IsProjectOutput(_projectDirectory, _project.AssemblyName, missingDebug), Is.False);
            Assert.That(resolved, Is.EqualTo(release));
        });
    }

    [Test]
    public void ResolveBuiltAssemblyPath_PreparedPathOutsideProjectBinFallsBackToBuiltOutputSearch()
    {
        string outsidePath = Path.Combine(_repositoryRoot, "outside", "Fixture.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(outsidePath)!);
        File.WriteAllText(outsidePath, "outside");
        string release = WriteOutput("Release", "net10.0", "Fixture");

        string? resolved = BuildStatePreparationService.ResolveBuiltAssemblyPath(
            CreateRequest(outsidePath), _project, _projectDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(BuildStatePreparationService.IsProjectOutput(_projectDirectory, _project.AssemblyName, outsidePath), Is.False);
            Assert.That(resolved, Is.EqualTo(release));
        });
    }

    [Test]
    public void ResolveBuiltAssemblyPath_PreparedPathWithDifferentAssemblyNameFallsBackToBuiltOutputSearch()
    {
        string otherAssembly = WriteOutput("Debug", "net10.0", "Other");
        string release = WriteOutput("Release", "net10.0", "Fixture");

        string? resolved = BuildStatePreparationService.ResolveBuiltAssemblyPath(
            CreateRequest(otherAssembly), _project, _projectDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(BuildStatePreparationService.IsProjectOutput(_projectDirectory, _project.AssemblyName, otherAssembly), Is.False);
            Assert.That(resolved, Is.EqualTo(release));
        });
    }

    [Test]
    public void ResolveBuiltAssemblyPath_ExplicitConfigurationBypassesPreparedOutput()
    {
        string debug = WriteOutput("Debug", "net10.0", "Fixture");
        string release = WriteOutput("Release", "net10.0", "Fixture");

        string? resolved = BuildStatePreparationService.ResolveBuiltAssemblyPath(
            CreateRequest(release, configuration: "Debug"), _project, _projectDirectory);

        Assert.That(resolved, Is.EqualTo(debug));
    }

    [Test]
    public void ResolveBuiltAssemblyPath_ExplicitTargetFrameworkBypassesPreparedOutput()
    {
        string net10 = WriteOutput("Debug", "net10.0", "Fixture");
        string net11 = WriteOutput("Debug", "net11.0", "Fixture");

        string? resolved = BuildStatePreparationService.ResolveBuiltAssemblyPath(
            CreateRequest(net11, targetFramework: "net10.0"), _project, _projectDirectory);

        Assert.That(resolved, Is.EqualTo(net10));
    }

    [Test]
    public void ResolveBuiltAssemblyPath_ExplicitRuntimeIdentifierBypassesPreparedOutput()
    {
        string windows = WriteOutput("Debug", "net10.0", "Fixture", "win-x64");
        string linux = WriteOutput("Debug", "net10.0", "Fixture", "linux-x64");

        string? resolved = BuildStatePreparationService.ResolveBuiltAssemblyPath(
            CreateRequest(linux, runtimeIdentifier: "win-x64"), _project, _projectDirectory);

        Assert.That(resolved, Is.EqualTo(windows));
    }

    private BuildStatePreflightRequest CreateRequest(
        string preparedPath,
        string? configuration = null,
        string? targetFramework = null,
        string? runtimeIdentifier = null)
    {
        ProjectDiscoveryResult discovery = new(
            ["Fixture"],
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = [_project]
        };
        BuildStateResolvedAssemblies resolution = new(Array.Empty<System.Reflection.Assembly>(), Array.Empty<string>())
        {
            ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [_project.AssemblyName] = preparedPath
            }
        };
        return new BuildStatePreflightRequest(
            _repositoryRoot,
            discovery,
            resolution,
            BuildPreparationMode.EnsureBuilt,
            RequestedConfiguration: configuration,
            RequestedTargetFramework: targetFramework,
            RequestedRuntimeIdentifier: runtimeIdentifier);
    }

    private string WriteOutput(string configuration, string targetFramework, string assemblyName, string? runtimeIdentifier = null)
    {
        string outputDirectory = runtimeIdentifier == null
            ? Path.Combine(_projectDirectory, "bin", configuration, targetFramework)
            : Path.Combine(_projectDirectory, "bin", configuration, targetFramework, runtimeIdentifier);
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, $"{assemblyName}.dll");
        File.WriteAllText(outputPath, $"{configuration}-{targetFramework}-{runtimeIdentifier}-{assemblyName}");
        return outputPath;
    }
}
