using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class BuildStatePreflightTests
{
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-buildstate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (!Directory.Exists(_repoRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(_repoRoot, true);
        }
        catch (IOException)
        {
            // Best-effort cleanup: on Windows, Assembly.LoadFrom (used by
            // BuildStatePreparationService.ResolveBuiltAssemblies, exercised by the
            // ensure-built integration test below) keeps its backing .dll file locked for the
            // lifetime of this process's default AssemblyLoadContext — the OS temp directory is
            // cleaned up independently, so a leftover locked file here is not a test failure.
        }
        catch (UnauthorizedAccessException)
        {
            // See above.
        }
    }

    [Test]
    public void ComputeBuildInputFingerprint_SameContent_ProducesSameDigest()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");

        string first = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);
        string second = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void ComputeBuildInputFingerprint_SourceContentChanges_ProducesDifferentDigest()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string before = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(projectPath)!, "Class1.cs"), "class C { int X; }");
        string after = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);

        Assert.That(after, Is.Not.EqualTo(before));
    }

    [Test]
    public void Evaluate_NoDiscoveredProjects_ReturnsEmptyNonBlockingResult()
    {
        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, ProjectDiscoveryResult.Empty, new BuildStateResolvedAssemblies(
                Array.Empty<Assembly>(), Array.Empty<string>()),
            BuildPreparationMode.Ordinary));

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.Blocked, Is.False);
    }

    [Test]
    public void Evaluate_AssemblyMissing_ReportsMissingArtifact()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = new(Array.Empty<Assembly>(), new[] { "Fixture" });

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.MissingArtifact));
        Assert.That(result.Diagnostics.Single().Evidence.BuildCommand, Does.Contain("dotnet build"));
    }

    [Test]
    public void Evaluate_ResolvedAssemblyWithoutReceipt_ReportsUnverifiableArtifact()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = SingleAssemblyResolution(assemblyPath);

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.UnverifiableArtifact));
    }

    [Test]
    public void Evaluate_ReceiptMatchesCurrentFingerprint_ReportsCurrent()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "Fixture", "Debug", "net10.0", fingerprint,
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath)));

        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = SingleAssemblyResolution(assemblyPath);

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary));

        Assert.That(result.Blocked, Is.False);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));
    }

    [Test]
    public void Evaluate_SourceChangedSinceReceipt_ReportsStaleArtifact()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        string staleFingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "Fixture", "Debug", "net10.0", staleFingerprint,
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath)));

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(projectPath)!, "Class1.cs"), "class C { int Y; }");

        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = SingleAssemblyResolution(assemblyPath);

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.StaleArtifact));
    }

    [Test]
    public void Evaluate_AssemblyOnDiskChangedSinceReceipt_ReportsStaleArtifact()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "Fixture", "Debug", "net10.0", fingerprint, "0000000000000000000000000000000000000000000000000000000000000000"));

        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = SingleAssemblyResolution(assemblyPath);

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.StaleArtifact));
    }

    [Test]
    public void Evaluate_RequestedConfigurationMismatchesReceipt_ReportsWrongConfiguration()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "Fixture", "Debug", "net10.0", fingerprint,
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath)));

        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = SingleAssemblyResolution(assemblyPath);

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary, RequestedConfiguration: "Release"));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.WrongConfiguration));
    }

    [Test]
    public void Evaluate_RequestedTargetFrameworkNotInProject_ReportsWrongTargetFramework()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture", targetFramework: "net10.0");

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), new[] { "Fixture" }),
            BuildPreparationMode.Ordinary, RequestedTargetFramework: "net8.0"));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.WrongTargetFramework));
    }

    [Test]
    public void Evaluate_ReceiptAssemblyNameMismatchesProject_ReportsWrongProjectOutput()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "SomeOtherAssembly", "Debug", "net10.0", "irrelevant",
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath)));

        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = SingleAssemblyResolution(assemblyPath);

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.WrongProjectOutput));
    }

    [Test]
    public void Evaluate_CancellationRequested_ReportsCancelled()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        using CancellationTokenSource cts = new();
        cts.Cancel();

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), new[] { "Fixture" }),
            BuildPreparationMode.Ordinary, CancellationToken: cts.Token));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Cancelled));
    }

    [Test]
    public void Evaluate_DependentProjectReferencesBlockedProject_ReportsInconsistentDependencyArtifact()
    {
        string upstreamPath = CreateProjectFixture("Upstream", "class U {}");
        string downstreamPath = CreateProjectFixture("Downstream", "class D {}");
        string downstreamAssembly = CreateFakeAssemblyFile("Downstream");
        string downstreamFingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(downstreamPath, _repoRoot);
        BuildReceiptStore.Write(downstreamAssembly, new BuildReceiptV1(
            downstreamPath, "Downstream", null, null, downstreamFingerprint,
            BuildStateCanonicalHasher.ComputeContentDigest(downstreamAssembly)));

        string relativeUpstreamPath = Path.GetRelativePath(Path.GetDirectoryName(downstreamPath)!, upstreamPath);
        ArchitectureDiscoveredProject downstreamProject = new(
            downstreamPath, "Downstream", new[] { "net10.0" })
        {
            ProjectReferences = new[] { new ArchitectureDiscoveredProjectReference(relativeUpstreamPath, downstreamPath) }
        };
        ArchitectureDiscoveredProject upstreamProject = new(upstreamPath, "Upstream", new[] { "net10.0" });

        ProjectDiscoveryResult discovery = new(
            new[] { "Downstream", "Upstream" }, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[] { downstreamProject, upstreamProject }
        };

        // Upstream is missing (never resolved) — Downstream's own artifact is otherwise current.
        BuildStateResolvedAssemblies resolution = new(
            new[] { LoadFakeAssembly(downstreamAssembly) }, new[] { "Upstream" });

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary));

        BuildStatePreflightDiagnostic downstreamDiagnostic =
            result.Diagnostics.Single(d => d.Evidence.ProjectPath == downstreamPath);
        Assert.That(downstreamDiagnostic.State, Is.EqualTo(BuildStatePreflightState.InconsistentDependencyArtifact));
    }

    [Test]
    public void Prepare_NoRestoreWithoutPriorRestore_ReportsRestoreRequired()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");

        var service = new BuildStatePreparationService();
        BuildStatePreflightResult result = service.Prepare(new BuildStatePreflightRequest(
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), Array.Empty<string>()),
            BuildPreparationMode.Ordinary, NoRestore: true));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.RestoreRequired));
    }

    [Test]
    public void Prepare_NoRestoreWithPriorRestoreArtifacts_DoesNotBlockOnRestore()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string objDirectory = Path.Combine(Path.GetDirectoryName(projectPath)!, "obj");
        Directory.CreateDirectory(objDirectory);
        File.WriteAllText(Path.Combine(objDirectory, "project.assets.json"), "{}");

        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = new(Array.Empty<Assembly>(), new[] { "Fixture" });

        var service = new BuildStatePreparationService();
        BuildStatePreflightResult result = service.Prepare(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary, NoRestore: true));

        // Restore prerequisites are satisfied, so preflight falls through to the ordinary
        // evaluator — the assembly is still missing, but for a different, more specific reason.
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.MissingArtifact));
    }

    [Test]
    [Category("Integration")]
    [CancelAfter(120_000)]
    public void Prepare_EnsureBuilt_BuildsOnceWritesReceiptAndReportsCurrent()
    {
        string projectPath = CreateRealBuildableProjectFixture("EnsureBuiltFixture");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "EnsureBuiltFixture");

        var service = new BuildStatePreparationService();
        BuildStatePreflightResult result = service.Prepare(new BuildStatePreflightRequest(
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), Array.Empty<string>()),
            BuildPreparationMode.EnsureBuilt, RequestedConfiguration: "Debug"));

        Assert.That(result.Blocked, Is.False, () => string.Join("; ", result.Diagnostics.Select(d => d.Evidence.Detail)));
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));

        string assemblyPath = Path.Combine(
            Path.GetDirectoryName(projectPath)!, "bin", "Debug", "net10.0", "EnsureBuiltFixture.dll");
        Assert.That(File.Exists(assemblyPath), Is.True);
        Assert.That(File.Exists(BuildReceiptStore.ReceiptPathFor(assemblyPath)), Is.True);
    }

    private string CreateRealBuildableProjectFixture(string assemblyName)
    {
        string projectDirectory = Path.Combine(_repoRoot, "src", assemblyName);
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, $"{assemblyName}.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable>" +
            "</PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(projectDirectory, "Class1.cs"), "namespace EnsureBuiltFixture; public class C {}");
        return projectPath;
    }

    private string CreateProjectFixture(string assemblyName, string sourceContent)
    {
        string projectDirectory = Path.Combine(_repoRoot, "src", assemblyName);
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, $"{assemblyName}.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(projectDirectory, "Class1.cs"), sourceContent);
        return projectPath;
    }

    private string CreateFakeAssemblyFile(string assemblyName)
    {
        string binDirectory = Path.Combine(_repoRoot, "src", assemblyName, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(binDirectory);
        string assemblyPath = Path.Combine(binDirectory, $"{assemblyName}.dll");
        File.WriteAllBytes(assemblyPath, System.Text.Encoding.UTF8.GetBytes($"fake-assembly-bytes:{assemblyName}"));
        return assemblyPath;
    }

    private static ProjectDiscoveryResult SingleProjectDiscovery(
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

    private static BuildStateResolvedAssemblies SingleAssemblyResolution(string assemblyPath)
    {
        return new BuildStateResolvedAssemblies(new[] { LoadFakeAssembly(assemblyPath) }, Array.Empty<string>());
    }

    // A real Assembly with a Location pointing at our fake .dll bytes, without requiring the
    // fixture to be a loadable managed assembly — this test's own assembly, reflection-only
    // "loaded" via LoadFrom is unnecessary: Assembly.Location is what the evaluator reads, and
    // .NET allows constructing a lightweight in-memory stand-in via Assembly.LoadFile only for
    // real PE files, so instead we reflect against this test assembly itself and override
    // nothing — callers only need GetName().Name and Location, both of which the currently
    // executing test assembly provides after being copied to the fixture path.
    private static Assembly LoadFakeAssembly(string assemblyPath)
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
