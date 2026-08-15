using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Category("E2E")]
public sealed class BuildStatePreflightTests
{
    private static readonly string[] _value = { "Fixture" };
    private static readonly string[] _value1 = { "Fixture" };
    private static readonly string[] _value2 = { "Fixture" };
    private static readonly string[] _value3 = { "net10.0" };
    private static readonly string[] _value4 = { "net10.0" };
    private static readonly string[] _value5 = { "Downstream", "Upstream" };
    private static readonly string[] _value6 = { "Upstream" };
    private static readonly string[] _value7 = { "Fixture" };
    private static readonly string[] _value8 = { "Fixture" };
    private static readonly string[] _value9 = { "Fixture" };
    private static readonly string[] _value10 = { "net10.0" };
    private static readonly string[] _value11 = { "net10.0" };
    private static readonly string[] _value12 = { "GraphApp", "GraphLib" };
    private static readonly string[] _staleManifestReasons = { "evaluated-msbuild-evidence-incomplete" };

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
        BuildStateResolvedAssemblies resolution = new(Array.Empty<Assembly>(), _value);

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.MissingArtifact));
        Assert.That(result.Diagnostics.Single().Evidence.BuildCommand, Does.Contain("dotnet build"));
        Assert.That(result.Diagnostics.Single().Evidence.CacheEligibility, Is.EqualTo("cache-ineligible"));
        Assert.That(result.Diagnostics.Single().Evidence.CacheIneligibilityReasons, Does.Contain("preflight-missingartifact"));
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
        Assert.That(result.Diagnostics.Single().Evidence.CacheEligibility, Is.EqualTo("cache-ineligible"));
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
    public void Evaluate_MatchingCacheEligibleReceipt_DoesNotReportManifestMismatch()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);
        EvaluatedBuildInputManifestV1 manifest = EvaluatedBuildInputManifestCollector.Collect(projectPath, _repoRoot);
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "Fixture", "Debug", "net10.0", fingerprint,
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath), manifest.Digest, manifest.Eligibility,
            manifest.IneligibilityReasons));

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, SingleProjectDiscovery(projectPath, "Fixture"), SingleAssemblyResolution(assemblyPath),
            BuildPreparationMode.Ordinary));

        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));
        Assert.That(manifest.Eligibility, Is.EqualTo(CacheEligibility.VerifiedCacheEligible));
        Assert.That(result.Diagnostics.Single().Evidence.CacheIneligibilityReasons, Is.Empty);
    }

    [Test]
    public void Evaluate_StaleEvaluatedManifestFingerprint_ReportsReceiptManifestMismatch()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "Fixture", "Debug", "net10.0", fingerprint,
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath), "stale-manifest-digest", CacheEligibility.CacheIneligible,
            _staleManifestReasons));

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, SingleProjectDiscovery(projectPath, "Fixture"), SingleAssemblyResolution(assemblyPath),
            BuildPreparationMode.Ordinary));

        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));
        Assert.That(result.Diagnostics.Single().Evidence.CacheIneligibilityReasons, Does.Contain("receipt-manifest-mismatch"));
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
    public void Evaluate_RequestedPlatformMismatchesReceipt_ReportsWrongConfiguration()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "Fixture", "Debug", "net10.0", fingerprint,
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath), Platform: "AnyCPU"));

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, SingleProjectDiscovery(projectPath, "Fixture"), SingleAssemblyResolution(assemblyPath),
            BuildPreparationMode.Ordinary, RequestedPlatform: "x64"));

        BuildStatePreflightDiagnostic diagnostic = result.Diagnostics.Single();
        Assert.That(diagnostic.State, Is.EqualTo(BuildStatePreflightState.WrongConfiguration));
        Assert.That(diagnostic.Evidence.RequestedConfiguration, Is.EqualTo("x64"));
        Assert.That(diagnostic.Evidence.ObservedConfiguration, Is.EqualTo("AnyCPU"));
    }

    [Test]
    public void Evaluate_RequestedRuntimeIdentifierMismatchesReceipt_ReportsWrongConfiguration()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "Fixture", "Debug", "net10.0", fingerprint,
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath), RuntimeIdentifier: "linux-x64"));

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, SingleProjectDiscovery(projectPath, "Fixture"), SingleAssemblyResolution(assemblyPath),
            BuildPreparationMode.Ordinary, RequestedRuntimeIdentifier: "win-x64"));

        BuildStatePreflightDiagnostic diagnostic = result.Diagnostics.Single();
        Assert.That(diagnostic.State, Is.EqualTo(BuildStatePreflightState.WrongConfiguration));
        Assert.That(diagnostic.Evidence.RequestedConfiguration, Is.EqualTo("win-x64"));
        Assert.That(diagnostic.Evidence.ObservedConfiguration, Is.EqualTo("linux-x64"));
    }

    [Test]
    public void Evaluate_RequestedTargetFrameworkNotInProjectButArtifactMissing_ReportsMissingArtifact()
    {
        // Per the normative precedence order, missing-artifact is checked before
        // wrong-target-framework — a project with no resolved assembly at all is reported as
        // missing-artifact even if it also declares an incompatible target framework.
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture", targetFramework: "net10.0");

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), _value1),
            BuildPreparationMode.Ordinary, RequestedTargetFramework: "net8.0"));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.MissingArtifact));
    }

    [Test]
    public void Evaluate_RequestedTargetFrameworkNotInProjectWithArtifactPresent_ReportsWrongTargetFramework()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture", targetFramework: "net10.0");
        BuildStateResolvedAssemblies resolution = SingleAssemblyResolution(assemblyPath);

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary, RequestedTargetFramework: "net8.0"));

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
    public void Evaluate_ReceiptProjectPathMismatchesProject_ReportsWrongProjectOutput()
    {
        // Matching AssemblyName alone is not enough to prove a receipt belongs to this project —
        // two different projects could share a simple assembly name, or a receipt could be
        // stale/misplaced from another location. ProjectPath must match too.
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            "src/SomewhereElse/Fixture.csproj", "Fixture", "Debug", "net10.0", "irrelevant",
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath)));

        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = SingleAssemblyResolution(assemblyPath);

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.WrongProjectOutput));
    }

    [Test]
    public void Evaluate_ReceiptMissingConfigurationWhenRequested_ReportsWrongConfigurationNotWildcardMatch()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("Fixture");
        string fingerprint = BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, _repoRoot);
        BuildReceiptStore.Write(assemblyPath, new BuildReceiptV1(
            projectPath, "Fixture", Configuration: null, TargetFramework: null, fingerprint,
            BuildStateCanonicalHasher.ComputeContentDigest(assemblyPath)));

        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = SingleAssemblyResolution(assemblyPath);

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary, RequestedConfiguration: "Release"));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.WrongConfiguration));
    }

    [Test]
    public void Evaluate_CancellationRequested_ReportsCancelled()
    {
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        using CancellationTokenSource cts = new();
        cts.Cancel();

        BuildStatePreflightResult result = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), _value2),
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

        // ArchitectureDiscoveredProjectReference.Path is produced by
        // ArchitectureProjectDiscoveryService in exactly the same canonical form as every
        // discovered project's own .Path (both go through the same GetRelativePath helper against
        // the repository root) — a reference is looked up by comparing it directly against
        // another project's .Path, not by combining it with a filesystem directory.
        ArchitectureDiscoveredProject downstreamProject = new(
            downstreamPath, "Downstream", _value3)
        {
            ProjectReferences = new[] { new ArchitectureDiscoveredProjectReference(upstreamPath, downstreamPath) }
        };
        ArchitectureDiscoveredProject upstreamProject = new(upstreamPath, "Upstream", _value4);

        ProjectDiscoveryResult discovery = new(
            _value5, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[] { downstreamProject, upstreamProject }
        };

        // Upstream is missing (never resolved) — Downstream's own artifact is otherwise current.
        BuildStateResolvedAssemblies resolution = new(
            new[] { LoadFakeAssembly(downstreamAssembly) }, _value6);

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
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), _value7),
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
        File.WriteAllText(Path.Combine(objDirectory, "project.assets.json"), """{"targets":{"net10.0":{}}}""");

        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = new(Array.Empty<Assembly>(), _value8);

        var service = new BuildStatePreparationService();
        BuildStatePreflightResult result = service.Prepare(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary, NoRestore: true));

        // Restore prerequisites are satisfied, so preflight falls through to the ordinary
        // evaluator — the assembly is still missing, but for a different, more specific reason.
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.MissingArtifact));
    }

    [Test]
    public void Prepare_NoRestoreWithTriviallyEmptyAssetsFile_ReportsRestoreRequired()
    {
        // A real `dotnet restore` never produces a bare `{}` — an assets file with no "targets"
        // object is not evidence of a completed restore and must not be accepted as one.
        string projectPath = CreateProjectFixture("Fixture", "class C {}");
        string objDirectory = Path.Combine(Path.GetDirectoryName(projectPath)!, "obj");
        Directory.CreateDirectory(objDirectory);
        File.WriteAllText(Path.Combine(objDirectory, "project.assets.json"), "{}");

        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "Fixture");
        BuildStateResolvedAssemblies resolution = new(Array.Empty<Assembly>(), _value9);

        var service = new BuildStatePreparationService();
        BuildStatePreflightResult result = service.Prepare(new BuildStatePreflightRequest(
            _repoRoot, discovery, resolution, BuildPreparationMode.Ordinary, NoRestore: true));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.RestoreRequired));
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

        Assert.That(result.Blocked, Is.False,
            () => string.Join(" | ", result.Diagnostics.Select(d => $"{d.State}: {d.Evidence.Detail}")));
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));

        string assemblyPath = Path.Combine(
            Path.GetDirectoryName(projectPath)!, "bin", "Debug", "net10.0", "EnsureBuiltFixture.dll");
        Assert.That(File.Exists(assemblyPath), Is.True);
        Assert.That(File.Exists(BuildReceiptStore.ReceiptPathFor(assemblyPath)), Is.True);
    }

    // Issue #375: cancellation during EnsureBuilt must terminate the in-flight child
    // dotnet restore/build process (without a shell — see BuildStatePreparationService's
    // ProcessStartInfo, which never sets UseShellExecute=true) and still remove the temporary
    // .slnx solution the existing `finally` block in InvokeGraphBuild already cleans up.
    [Test]
    [Category("Integration")]
    [CancelAfter(120_000)]
    public async Task Prepare_EnsureBuilt_CancelledMidBuild_TerminatesProcessAndCleansUpTempSolution()
    {
        string projectPath = CreateRealBuildableProjectFixture("CancelledBuildFixture");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "CancelledBuildFixture");
        var service = new BuildStatePreparationService();

        const string TempSolutionPattern = "archlinternet-ensure-built-*.slnx";
        int tempSolutionCountBefore = Directory.GetFiles(Path.GetTempPath(), TempSolutionPattern).Length;

        using CancellationTokenSource cts = new();
        Task prepareTask = Task.Run(() => service.Prepare(new BuildStatePreflightRequest(
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), Array.Empty<string>()),
            BuildPreparationMode.EnsureBuilt, RequestedConfiguration: "Debug", CancellationToken: cts.Token)));

        // Give the child dotnet restore/build process a moment to actually start before
        // cancelling — cancelling instantly risks interrupting before Process.Start even runs,
        // which would still throw OperationCanceledException but wouldn't exercise the
        // process-kill path this test is for.
        await Task.Delay(300);
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await prepareTask);

        int tempSolutionCountAfter = Directory.GetFiles(Path.GetTempPath(), TempSolutionPattern).Length;
        Assert.That(tempSolutionCountAfter, Is.EqualTo(tempSolutionCountBefore));
    }

    // A child that survives Process.Kill(entireProcessTree: true) for the full post-kill
    // deadline (a hostile or kernel-stuck process) is not something a real dotnet build can be
    // made to reproduce deterministically. WaitForExitOrCancellationCore is fake-delegate
    // testable specifically so this timeout branch can be proven without flaking.
    [Test]
    public void WaitForExitOrCancellationCore_ProcessSurvivesKillPastDeadline_ThrowsCleanupTimeoutException()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        bool killed = false;

        BuildStateProcessCleanupTimedOutException? thrown = Assert.Throws<BuildStateProcessCleanupTimedOutException>(() =>
            BuildStatePreparationService.WaitForExitOrCancellationCore(
                waitForExit: _ => false,
                killProcessTree: () => killed = true,
                cancellationToken: cts.Token,
                processId: 4242));

        Assert.That(killed, Is.True);
        Assert.That(thrown!.ProcessId, Is.EqualTo(4242));
        Assert.That(thrown.TimeoutMs, Is.GreaterThan(0));
    }

    [Test]
    public void WaitForExitOrCancellationCore_ProcessExitsWithinKillDeadline_ThrowsPlainOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        bool killed = false;

        Assert.Throws<OperationCanceledException>(() =>
            BuildStatePreparationService.WaitForExitOrCancellationCore(
                waitForExit: _ => killed, // exits only after the kill has been requested
                killProcessTree: () => killed = true,
                cancellationToken: cts.Token,
                processId: 4242));
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

    [Test]
    [Category("Integration")]
    [CancelAfter(180_000)]
    public void Prepare_EnsureBuilt_MultiProjectGraphWithReference_BuildsBothViaSingleSolutionInvocation()
    {
        // EnsureBuilt now builds the whole selected graph via one generated temporary .slnx
        // solution and a single `dotnet build` invocation, instead of looping per discovered
        // project — this proves App (which references Lib) and Lib both come out Current from
        // that one build, including the shared/referenced project.
        string libPath = CreateRealBuildableProjectFixture("GraphLib");
        string appDir = Path.Combine(_repoRoot, "src", "GraphApp");
        Directory.CreateDirectory(appDir);
        string appPath = Path.Combine(appDir, "GraphApp.csproj");
        File.WriteAllText(appPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>" +
            "</PropertyGroup><ItemGroup><ProjectReference Include=\"../GraphLib/GraphLib.csproj\" /></ItemGroup></Project>");
        File.WriteAllText(Path.Combine(appDir, "Program.cs"), "System.Console.WriteLine(typeof(EnsureBuiltFixture.C));");

        ArchitectureDiscoveredProject libProject = new(
            Path.GetRelativePath(_repoRoot, libPath).Replace('\\', '/'), "GraphLib", _value10);
        ArchitectureDiscoveredProject appProject = new(
            Path.GetRelativePath(_repoRoot, appPath).Replace('\\', '/'), "GraphApp", _value11)
        {
            ProjectReferences = new[]
            {
                new ArchitectureDiscoveredProjectReference(libProject.Path, Path.GetRelativePath(_repoRoot, appPath).Replace('\\', '/'))
            }
        };

        ProjectDiscoveryResult discovery = new(
            _value12, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[] { appProject, libProject }
        };

        var service = new BuildStatePreparationService();
        BuildStatePreflightResult result = service.Prepare(new BuildStatePreflightRequest(
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), Array.Empty<string>()),
            BuildPreparationMode.EnsureBuilt, RequestedConfiguration: "Debug"));

        Assert.That(result.Blocked, Is.False, () => string.Join("; ", result.Diagnostics.Select(d => d.Evidence.Detail)));
        Assert.That(result.Diagnostics, Has.Count.EqualTo(2));
        Assert.That(result.Diagnostics, Has.All.Matches<BuildStatePreflightDiagnostic>(
            d => d.State == BuildStatePreflightState.Current));
    }

    [Test]
    [Category("Integration")]
    [CancelAfter(180_000)]
    // On Windows, Assembly.LoadFrom (used by ResolveBuiltAssemblies so Assembly.Location stays
    // populated for the evaluator) keeps an exclusive handle on the loaded .dll for the life of
    // this process. This test calls Prepare(EnsureBuilt) twice against the same output path in
    // one process, so the second dotnet build's file-copy step can fail to overwrite a file the
    // first Prepare() call already loaded — a real, documented limitation (see
    // BuildStatePreflightAssemblyReloadTests), not something to paper over with a retry here.
    [Platform(Exclude = "Win", Reason = "Assembly.LoadFrom locks the .dll for the process lifetime; a second same-process rebuild can't overwrite it.")]
    public void Prepare_EnsureBuiltAfterSourceChange_OverwritesStaleReceiptAndReportsCurrent()
    {
        string projectPath = CreateRealBuildableProjectFixture("RebuildFixture");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "RebuildFixture");
        var service = new BuildStatePreparationService();

        BuildStatePreflightResult firstBuild = service.Prepare(new BuildStatePreflightRequest(
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), Array.Empty<string>()),
            BuildPreparationMode.EnsureBuilt, RequestedConfiguration: "Debug"));
        Assert.That(firstBuild.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));

        string assemblyPath = Path.Combine(
            Path.GetDirectoryName(projectPath)!, "bin", "Debug", "net10.0", "RebuildFixture.dll");
        BuildStateResolvedAssemblies builtResolution = new(new[] { LoadFakeAssembly(assemblyPath) }, Array.Empty<string>());

        File.WriteAllText(
            Path.Combine(Path.GetDirectoryName(projectPath)!, "Class1.cs"),
            "namespace RebuildFixture; public class C { public int Changed; }");

        BuildStatePreflightResult afterSourceChange = BuildStatePreflightEvaluator.Evaluate(new BuildStatePreflightRequest(
            _repoRoot, discovery, builtResolution,
            BuildPreparationMode.Ordinary, RequestedConfiguration: "Debug"));
        Assert.That(afterSourceChange.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.StaleArtifact));

        BuildStatePreflightResult secondBuild = service.Prepare(new BuildStatePreflightRequest(
            _repoRoot, discovery, new BuildStateResolvedAssemblies(Array.Empty<Assembly>(), Array.Empty<string>()),
            BuildPreparationMode.EnsureBuilt, RequestedConfiguration: "Debug"));

        // Rebuilding after a source change must overwrite the now-stale receipt so a subsequent
        // ordinary preflight check sees Current again, not the same StaleArtifact result it saw
        // immediately before this rebuild.
        Assert.That(secondBuild.Blocked, Is.False);
        Assert.That(secondBuild.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));
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
    private static FakeAssembly LoadFakeAssembly(string assemblyPath)
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
