using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

// Fake-composition tests for the public-api application seam. The session is built over the real
// test assembly, so capture exercises the actual reflection scanner rather than a stubbed surface.
[TestFixture]
public sealed class ArchitecturePublicApiApplicationServiceTests
{
    private const string ContractId = "surface";
    private const string PolicyPath = "architecture/dependencies.arch.yml";
    private const string SnapshotPath = "architecture/api/surface.txt";
    private const string CleanDeclaredTypeName = "PublicApiSurfaceContractTestFixtures.CleanDeclaredType";

    private static string AssemblyName => typeof(ArchitecturePublicApiApplicationServiceTests).Assembly.GetName().Name!;

    private static ArchitecturePublicApiSurfaceContract Contract(
        string? apiSnapshot = null, params string[] declaredApi)
    {
        return new ArchitecturePublicApiSurfaceContract
        {
            Id = ContractId,
            Name = ContractId,
            Assemblies = new List<string> { AssemblyName },
            ApiSnapshot = apiSnapshot,
            DeclaredApi = declaredApi.ToList(),
        };
    }

    private static ArchitectureContractDocument Document(ArchitecturePublicApiSurfaceContract contract)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { AssemblyName },
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract> { contract },
            },
        };
    }

    private static ArchitectureAnalysisSession Session(
        ArchitectureContractDocument document,
        IReadOnlyCollection<Assembly>? targetAssemblies = null,
        ProjectDiscoveryResult? discovery = null)
    {
        var context = new ArchitectureAnalysisContext(
            "/fake/repository/root",
            targetAssemblies ?? new[] { typeof(ArchitecturePublicApiApplicationServiceTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);

        return new ArchitectureAnalysisSession(
            context, document, selectedContractIds: null, enableUnmatchedIgnoreTracking: true,
            preprocessorSymbols: null);
    }

    private static ArchitecturePublicApiApplicationService Service(
        ArchitectureContractDocument document,
        FakePublicApiSnapshotStore store,
        IReadOnlyCollection<Assembly>? targetAssemblies = null,
        ProjectDiscoveryResult? discovery = null,
        BuildStatePreflightResult? preflight = null)
    {
        var runnerSetupService = new FakeRunnerSetupService
        {
            DocumentToReturn = document,
            RunnerToReturn = new FakeContractRunner(Session(document, targetAssemblies, discovery)),
        };

        return new ArchitecturePublicApiApplicationService(
            runnerSetupService, new FakeBuildStatePreparationService(preflight), store);
    }

    private static IReadOnlyList<PublicApiSnapshotEntry> CapturedEntries(string snapshot)
    {
        return PublicApiSnapshotFormat.Parse(snapshot, SnapshotPath).Entries;
    }

    [Test]
    public void Capture_ProducesParsableDeterministicSnapshot()
    {
        ArchitectureContractDocument document = Document(Contract());
        var store = new FakePublicApiSnapshotStore();

        PublicApiCaptureOutcome first = Service(document, store).Capture(
            new PublicApiCaptureRequest { PolicyPath = PolicyPath, ContractId = ContractId, OutputPath = SnapshotPath });
        PublicApiCaptureOutcome second = Service(document, store).Capture(
            new PublicApiCaptureRequest { PolicyPath = PolicyPath, ContractId = ContractId, OutputPath = SnapshotPath });

        Assert.Multiple(() =>
        {
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.EntryCount, Is.GreaterThan(0));
            Assert.That(first.Snapshot, Is.EqualTo(second.Snapshot));
            Assert.That(
                CapturedEntries(first.Snapshot!).Select(entry => entry.Signature),
                Has.Some.EqualTo($"class {CleanDeclaredTypeName} [sealed]"),
                "A captured snapshot records the exact grammar, so a sealed type carries its modifier.");
        });
    }

    [Test]
    public void Capture_UnknownContractId_FailsListingAvailableIds()
    {
        PublicApiCaptureOutcome outcome = Service(Document(Contract()), new FakePublicApiSnapshotStore()).Capture(
            new PublicApiCaptureRequest { PolicyPath = PolicyPath, ContractId = "absent", OutputPath = SnapshotPath });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("Unknown public API surface contract 'absent'"));
            Assert.That(outcome.Error, Does.Contain(ContractId));
        });
    }

    [Test]
    public void Capture_AuditContractIsAlsoResolvable()
    {
        ArchitecturePublicApiSurfaceContract contract = Contract();
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { AssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                AuditPublicApiSurface = new List<ArchitecturePublicApiSurfaceContract> { contract },
            },
        };

        PublicApiCaptureOutcome outcome = Service(document, new FakePublicApiSnapshotStore()).Capture(
            new PublicApiCaptureRequest { PolicyPath = PolicyPath, ContractId = ContractId, OutputPath = SnapshotPath });

        Assert.That(outcome.Succeeded, Is.True);
    }

    [Test]
    public void Capture_UnresolvedAssembly_FailsNamingIt()
    {
        PublicApiCaptureOutcome outcome = Service(
            Document(Contract()), new FakePublicApiSnapshotStore(), targetAssemblies: Array.Empty<Assembly>()).Capture(
            new PublicApiCaptureRequest { PolicyPath = PolicyPath, ContractId = ContractId, OutputPath = SnapshotPath });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("could not be resolved"));
            Assert.That(outcome.Error, Does.Contain(AssemblyName));
        });
    }

    [Test]
    public void Capture_BlockedPreflight_FailsWithDiagnosticsAndNoSnapshot()
    {
        ProjectDiscoveryResult discovery = new(
            new[] { AssemblyName }, Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject("Test.csproj", AssemblyName, new[] { "net10.0" }),
            },
        };

        BuildStatePreflightResult blocked = new(new[]
        {
            new BuildStatePreflightDiagnostic(
                AssemblyName, null, BuildStatePreflightState.MissingArtifact,
                new BuildStatePreflightEvidence("Test.csproj", AssemblyName)),
        });

        PublicApiCaptureOutcome outcome = Service(
            Document(Contract()), new FakePublicApiSnapshotStore(), discovery: discovery, preflight: blocked).Capture(
            new PublicApiCaptureRequest { PolicyPath = PolicyPath, ContractId = ContractId, OutputPath = SnapshotPath });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Snapshot, Is.Null);
            Assert.That(outcome.Error, Does.Contain("Build state preflight is blocked"));
            Assert.That(outcome.PreflightDiagnostics, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Diff_SnapshotMatchingLiveSurface_ReportsInSync()
    {
        ArchitectureContractDocument document = Document(Contract());
        var store = new FakePublicApiSnapshotStore();
        PublicApiCaptureOutcome captured = Service(document, store).Capture(
            new PublicApiCaptureRequest { PolicyPath = PolicyPath, ContractId = ContractId, OutputPath = SnapshotPath });
        store.Entries = CapturedEntries(captured.Snapshot!);

        PublicApiDiffOutcome outcome = Service(document, store).Diff(new PublicApiDiffRequest
        {
            PolicyPath = PolicyPath,
            ContractId = ContractId,
            SnapshotPath = SnapshotPath,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(outcome.InSync, Is.True);
            Assert.That(outcome.Delta.HasChanges, Is.False);
        });
    }

    [Test]
    public void Diff_DriftedSnapshot_SeparatesAdditionsRemovalsAndChanges()
    {
        ArchitectureContractDocument document = Document(Contract());
        var store = new FakePublicApiSnapshotStore();
        PublicApiCaptureOutcome captured = Service(document, store).Capture(
            new PublicApiCaptureRequest { PolicyPath = PolicyPath, ContractId = ContractId, OutputPath = SnapshotPath });

        List<PublicApiSnapshotEntry> declared = CapturedEntries(captured.Snapshot!).ToList();
        declared.RemoveAll(entry => entry.Signature == $"class {CleanDeclaredTypeName} [sealed]");
        declared.RemoveAll(entry => entry.Signature == $"method {CleanDeclaredTypeName}.DoWork(): System.Void [static]");
        declared.Add(new PublicApiSnapshotEntry(
            AssemblyName, $"method {CleanDeclaredTypeName}.DoWork(): System.Int32 [static]"));
        declared.Add(new PublicApiSnapshotEntry(AssemblyName, "class PublicApiSurfaceContractTestFixtures.NeverExisted"));
        store.Entries = declared;

        PublicApiDiffOutcome outcome = Service(document, store).Diff(new PublicApiDiffRequest
        {
            PolicyPath = PolicyPath,
            ContractId = ContractId,
            SnapshotPath = SnapshotPath,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.InSync, Is.False);
            Assert.That(
                outcome.Delta.Added.Select(entry => entry.Signature),
                Does.Contain($"class {CleanDeclaredTypeName} [sealed]"));
            Assert.That(
                outcome.Delta.Removed.Select(entry => entry.Signature),
                Does.Contain("class PublicApiSurfaceContractTestFixtures.NeverExisted"));
            Assert.That(
                outcome.Delta.Changed.Select(entry => entry.PreviousSignature),
                Does.Contain($"method {CleanDeclaredTypeName}.DoWork(): System.Int32 [static]"));
        });
    }

    [Test]
    public void Diff_MissingSnapshotFile_FailsWithCaptureGuidance()
    {
        var store = new FakePublicApiSnapshotStore { Exists = false };

        PublicApiDiffOutcome outcome = Service(Document(Contract()), store).Diff(new PublicApiDiffRequest
        {
            PolicyPath = PolicyPath,
            ContractId = ContractId,
            SnapshotPath = SnapshotPath,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("Public API snapshot not found"));
            Assert.That(outcome.Error, Does.Contain("public-api capture"));
        });
    }

    [Test]
    public void Diff_UnreadableSnapshot_SurfacesTheParseError()
    {
        var store = new FakePublicApiSnapshotStore { ReadError = "unsupported snapshot version '9'" };

        PublicApiDiffOutcome outcome = Service(Document(Contract()), store).Diff(new PublicApiDiffRequest
        {
            PolicyPath = PolicyPath,
            ContractId = ContractId,
            SnapshotPath = SnapshotPath,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("unsupported snapshot version '9'"));
        });
    }

    [Test]
    public void Diff_EscapingSnapshotPath_Fails()
    {
        var store = new FakePublicApiSnapshotStore { ResolveError = "resolves outside the policy boundary" };

        PublicApiDiffOutcome outcome = Service(Document(Contract()), store).Diff(new PublicApiDiffRequest
        {
            PolicyPath = PolicyPath,
            ContractId = ContractId,
            SnapshotPath = "../escape.txt",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("outside the policy boundary"));
        });
    }

    [Test]
    public void Update_InlineDeclarationWithoutSnapshot_RefusesAndNamesMigration()
    {
        PublicApiUpdateOutcome outcome = Service(
            Document(Contract(apiSnapshot: null, "class Whatever")), new FakePublicApiSnapshotStore()).Update(
            new PublicApiUpdateRequest
            {
                PolicyPath = PolicyPath,
                ContractId = ContractId,
                SnapshotPath = SnapshotPath,
            });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Snapshot, Is.Null);
            Assert.That(outcome.Error, Does.Contain("cannot preserve the surrounding policy comments"));
            Assert.That(outcome.Error, Does.Contain("public-api migrate"));
        });
    }

    [Test]
    public void Update_SnapshotBackedContract_ReturnsSnapshotAndDelta()
    {
        ArchitecturePublicApiSurfaceContract contract = Contract(apiSnapshot: SnapshotPath);
        contract.ResolvedSnapshotEntries = new[]
        {
            new PublicApiSnapshotEntry(AssemblyName, "class PublicApiSurfaceContractTestFixtures.NeverExisted"),
        };

        PublicApiUpdateOutcome outcome = Service(Document(contract), new FakePublicApiSnapshotStore()).Update(
            new PublicApiUpdateRequest
            {
                PolicyPath = PolicyPath,
                ContractId = ContractId,
                SnapshotPath = SnapshotPath,
                DryRun = true,
            });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True, outcome.Error);
            Assert.That(outcome.DryRun, Is.True);
            Assert.That(outcome.Snapshot, Does.Contain($"class {CleanDeclaredTypeName} [sealed]"));
            Assert.That(outcome.ResolvedSnapshotPath, Is.Not.Null);
            Assert.That(
                outcome.Delta.Removed.Select(entry => entry.Signature),
                Does.Contain("class PublicApiSurfaceContractTestFixtures.NeverExisted"));
        });
    }

    // The contract's snapshot legitimately does not exist yet between the first capture and the
    // first update, so that specific recorded error must not block the write.
    [Test]
    public void Update_MissingSnapshotFile_IsAllowedForTheContractsOwnSnapshot()
    {
        ArchitecturePublicApiSurfaceContract contract = Contract(apiSnapshot: SnapshotPath);
        contract.ApiSnapshotError = "references a public API snapshot 'x' that does not exist (resolved to 'y').";

        PublicApiUpdateOutcome outcome = Service(Document(contract), new FakePublicApiSnapshotStore()).Update(
            new PublicApiUpdateRequest
            {
                PolicyPath = PolicyPath,
                ContractId = ContractId,
                SnapshotPath = SnapshotPath,
            });

        Assert.That(outcome.Succeeded, Is.True, outcome.Error);
    }

    [Test]
    public void Update_UnreadableSnapshot_RefusesToReplaceIt()
    {
        ArchitecturePublicApiSurfaceContract contract = Contract(apiSnapshot: SnapshotPath);
        contract.ApiSnapshotError = "has a public API snapshot 'x' captured for contract 'other'.";

        PublicApiUpdateOutcome outcome = Service(Document(contract), new FakePublicApiSnapshotStore()).Update(
            new PublicApiUpdateRequest
            {
                PolicyPath = PolicyPath,
                ContractId = ContractId,
                SnapshotPath = SnapshotPath,
            });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Snapshot, Is.Null);
            Assert.That(outcome.FailureKind, Is.EqualTo(PublicApiFailureKind.InvalidInput));
            Assert.That(outcome.Error, Does.Contain("captured for contract 'other'"));
        });
    }

    [Test]
    public void Update_SnapshotPathOtherThanTheContractsOwn_IsRefused()
    {
        ArchitecturePublicApiSurfaceContract contract = Contract(apiSnapshot: SnapshotPath);
        contract.ResolvedSnapshotPath = Path.Combine("/fake/repository/root", "architecture", "api", "other.txt");

        PublicApiUpdateOutcome outcome = Service(Document(contract), new FakePublicApiSnapshotStore()).Update(
            new PublicApiUpdateRequest
            {
                PolicyPath = PolicyPath,
                ContractId = ContractId,
                SnapshotPath = SnapshotPath,
            });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("does not resolve to the snapshot declared by contract"));
        });
    }

    [Test]
    public void Update_UnknownContract_Fails()
    {
        PublicApiUpdateOutcome outcome = Service(Document(Contract()), new FakePublicApiSnapshotStore()).Update(
            new PublicApiUpdateRequest
            {
                PolicyPath = PolicyPath,
                ContractId = "absent",
                SnapshotPath = SnapshotPath,
            });

        Assert.That(outcome.Error, Does.Contain("Unknown public API surface contract"));
    }

    [Test]
    public void Migrate_InlineListMatchingLiveSurface_WritesWithoutDrift()
    {
        ArchitectureContractDocument document = Document(Contract());
        var store = new FakePublicApiSnapshotStore();
        PublicApiCaptureOutcome captured = Service(document, store).Capture(
            new PublicApiCaptureRequest { PolicyPath = PolicyPath, ContractId = ContractId, OutputPath = SnapshotPath });

        ArchitecturePublicApiSurfaceContract contract = Contract();
        // The inline list is authored in the legacy identity grammar, so the captured exact
        // signatures are stripped back to their base form before being compared.
        contract.DeclaredApi = CapturedEntries(captured.Snapshot!)
            .Select(entry => Scanning.ArchitecturePublicApiSignatureDetails.StripDetails(entry.Signature))
            .Distinct()
            .ToList();

        PublicApiMigrateOutcome outcome = Service(Document(contract), store).Migrate(new PublicApiMigrateRequest
        {
            PolicyPath = PolicyPath,
            ContractId = ContractId,
            OutputPath = SnapshotPath,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(outcome.HasDrift, Is.False);
            Assert.That(outcome.Snapshot, Is.Not.Null);
        });
    }

    [Test]
    public void Migrate_DriftWithoutAcceptance_RefusesAndListsBothSides()
    {
        PublicApiMigrateOutcome outcome = Service(
            Document(Contract(apiSnapshot: null, "class PublicApiSurfaceContractTestFixtures.NeverExisted")),
            new FakePublicApiSnapshotStore()).Migrate(new PublicApiMigrateRequest
            {
                PolicyPath = PolicyPath,
                ContractId = ContractId,
                OutputPath = SnapshotPath,
            });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Snapshot, Is.Null);
            Assert.That(outcome.HasDrift, Is.True);
            Assert.That(
                outcome.StaleDeclarations,
                Does.Contain("class PublicApiSurfaceContractTestFixtures.NeverExisted"));
            Assert.That(outcome.UndeclaredSurface, Is.Not.Empty);
            Assert.That(outcome.Error, Does.Contain("silently accept that drift"));
            Assert.That(outcome.FailureKind, Is.EqualTo(PublicApiFailureKind.Drift));
        });
    }

    [Test]
    public void Migrate_AcceptedDrift_WritesAndStillReportsDrift()
    {
        PublicApiMigrateOutcome outcome = Service(
            Document(Contract(apiSnapshot: null, "class PublicApiSurfaceContractTestFixtures.NeverExisted")),
            new FakePublicApiSnapshotStore()).Migrate(new PublicApiMigrateRequest
            {
                PolicyPath = PolicyPath,
                ContractId = ContractId,
                OutputPath = SnapshotPath,
                AcceptDrift = true,
            });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(outcome.Snapshot, Does.Contain($"class {CleanDeclaredTypeName} [sealed]"));
            Assert.That(outcome.HasDrift, Is.True);
            Assert.That(
                outcome.StaleDeclarations,
                Does.Contain("class PublicApiSurfaceContractTestFixtures.NeverExisted"));
        });
    }

    [Test]
    public void Migrate_EscapingOutputPath_FailsBeforeWriting()
    {
        var store = new FakePublicApiSnapshotStore { ResolveError = "resolves outside the policy boundary" };

        PublicApiMigrateOutcome outcome = Service(Document(Contract()), store).Migrate(new PublicApiMigrateRequest
        {
            PolicyPath = PolicyPath,
            ContractId = ContractId,
            OutputPath = "../escape.txt",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Snapshot, Is.Null);
            Assert.That(outcome.Error, Does.Contain("outside the policy boundary"));
        });
    }

    [Test]
    public void Migrate_UnknownContract_Fails()
    {
        PublicApiMigrateOutcome outcome = Service(Document(Contract()), new FakePublicApiSnapshotStore()).Migrate(
            new PublicApiMigrateRequest
            {
                PolicyPath = PolicyPath,
                ContractId = "absent",
                OutputPath = SnapshotPath,
            });

        Assert.That(outcome.Error, Does.Contain("Unknown public API surface contract"));
    }

    // On Windows and macOS's default (case-insensitive) filesystems, 'api/Surface.txt' and
    // 'api/surface.txt' name the same file. On Linux's common ext4 they do not: treating them as
    // equal there would let update silently rewrite a different file than it just read and diffed
    // against, while reporting success against the path the caller actually asked for.
    [Test]
    public void PathsMatch_CaseDifference_IsOsAware()
    {
        bool expectCaseInsensitive = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

        bool matches = ArchitecturePublicApiApplicationService.PathsMatch(
            "/repo/architecture/api/Surface.txt", "/repo/architecture/api/surface.txt");

        Assert.That(matches, Is.EqualTo(expectCaseInsensitive));
    }

    [Test]
    public void PathsMatch_IdenticalPaths_AlwaysMatch()
    {
        Assert.That(
            ArchitecturePublicApiApplicationService.PathsMatch(
                "/repo/architecture/api/surface.txt", "/repo/architecture/api/surface.txt"),
            Is.True);
    }

    private sealed class FakePublicApiSnapshotStore : IPublicApiSnapshotStore
    {
        public IReadOnlyList<PublicApiSnapshotEntry> Entries { get; set; } = Array.Empty<PublicApiSnapshotEntry>();

        public bool Exists { get; set; } = true;

        public string? ResolveError { get; set; }

        public string? ReadError { get; set; }

        public string ResolvePath(string policyPath, string snapshotPath)
        {
            return ResolveError == null
                ? Path.Combine("/fake/repository/root", snapshotPath)
                : throw new InvalidOperationException(ResolveError);
        }

        bool IPublicApiSnapshotStore.Exists(string resolvedPath) => Exists;

        public PublicApiSnapshotDocument Read(string resolvedPath, string authoredPath)
        {
            return ReadError == null
                ? new PublicApiSnapshotDocument(PublicApiSnapshotFormat.CurrentVersion, ContractId, Entries)
                : throw new InvalidOperationException(ReadError);
        }
    }

    private sealed class FakeBuildStatePreparationService(BuildStatePreflightResult? result) : IBuildStatePreparationService
    {
        public BuildStatePreflightResult Prepare(BuildStatePreflightRequest request)
        {
            return result ?? new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }
    }
}
