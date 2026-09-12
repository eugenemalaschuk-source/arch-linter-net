using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Shared fixture lifecycle and builders for the build-state preflight tests.
/// This support type intentionally contains no NUnit test methods, so inheriting fixtures
/// do not execute another fixture's scenarios.
/// </summary>
public abstract class BuildStatePreflightTestSupport
{
    protected string RepositoryRoot { get; private set; } = null!;

    [SetUp]
    public void SetUpBuildStateFixture()
    {
        RepositoryRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-buildstate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RepositoryRoot);
    }

    [TearDown]
    public void TearDownBuildStateFixture()
    {
        if (!Directory.Exists(RepositoryRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(RepositoryRoot, true);
        }
        catch (IOException)
        {
            // Best-effort cleanup: on Windows, Assembly.LoadFrom (used by
            // BuildStateRuntimeBuildPreparation.ResolveBuiltAssemblies) keeps its backing .dll
            // locked for the lifetime of this process's default AssemblyLoadContext. The OS
            // temp directory is cleaned up independently, so a leftover locked file is not a
            // test failure.
        }
        catch (UnauthorizedAccessException)
        {
            // See above.
        }
    }

    protected string CreateProjectFixture(string assemblyName, string sourceContent)
    {
        string projectDirectory = Path.Combine(RepositoryRoot, "src", assemblyName);
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, $"{assemblyName}.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(projectDirectory, "Class1.cs"), sourceContent);
        return projectPath;
    }

    protected string CreateFakeAssemblyFile(string assemblyName)
    {
        string binDirectory = Path.Combine(RepositoryRoot, "src", assemblyName, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(binDirectory);
        string assemblyPath = Path.Combine(binDirectory, $"{assemblyName}.dll");
        File.WriteAllBytes(assemblyPath, System.Text.Encoding.UTF8.GetBytes($"fake-assembly-bytes:{assemblyName}"));
        return assemblyPath;
    }

    protected static ProjectDiscoveryResult SingleProjectDiscovery(
        string projectPath, string assemblyName, string targetFramework = "net10.0")
    {
        return new ProjectDiscoveryResult(
            new[] { assemblyName }, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject(projectPath, assemblyName, new[] { targetFramework })
            }
        };
    }

    protected static BuildStateResolvedAssemblies SingleAssemblyResolution(string assemblyPath)
    {
        return new BuildStateResolvedAssemblies(new[] { LoadFakeAssembly(assemblyPath) }, Array.Empty<string>());
    }

    // A real Assembly with a Location pointing at our fake .dll bytes, without requiring the
    // fixture to be a loadable managed assembly. Callers only need GetName().Name and Location.
    protected static Assembly LoadFakeAssembly(string assemblyPath)
    {
        return new FakeAssembly(assemblyPath);
    }

    private sealed class FakeAssembly : Assembly
    {
        private readonly string _location;
        private readonly AssemblyName _name;

        public FakeAssembly(string location)
        {
            _location = location;
            _name = new AssemblyName(Path.GetFileNameWithoutExtension(location));
        }

        public override string Location => _location;

        public override AssemblyName GetName() => _name;

        public override AssemblyName GetName(bool copiedName) => _name;
    }
}
