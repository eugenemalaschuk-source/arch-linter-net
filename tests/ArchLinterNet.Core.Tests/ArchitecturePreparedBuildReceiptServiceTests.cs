using System.Reflection;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePreparedBuildReceiptServiceTests : BuildStatePreflightTestSupport
{
    [SetUp]
    public void ResetBuildInstrumentation()
    {
        BuildStateRuntimeBuildProcessExecutor.GraphBuildOverride = null;
    }

    [TearDown]
    public void ClearBuildInstrumentation()
    {
        BuildStateRuntimeBuildProcessExecutor.GraphBuildOverride = null;
    }

    [Test]
    public void Publish_BuildsAndPublishesInsideAuthoritativePath_AndOrdinaryFanoutDoesNotBuildAgain()
    {
        string projectPath = CreateProjectFixture("PreparedCandidateFixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("PreparedCandidateFixture");
        string objDirectory = Path.Combine(Path.GetDirectoryName(projectPath)!, "obj");
        Directory.CreateDirectory(objDirectory);
        File.WriteAllText(Path.Combine(objDirectory, "project.assets.json"), "{\"targets\":{\"net10.0\":{}}}");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "PreparedCandidateFixture") with
        {
            ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PreparedCandidateFixture"] = assemblyPath,
            },
        };
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Prepared candidate test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Configuration = "Debug",
                TargetFramework = "net10.0",
            },
        };
        ArchitectureRunnerPreparation preparation = new(
            RepositoryRoot,
            PreprocessorSymbols: null,
            discovery,
            ResolveAssemblyOutputs: true,
            SelectedAssemblyArtifactPaths: new[] { assemblyPath },
            CapturedArtifactContentDigests: new Dictionary<string, string>(StringComparer.Ordinal),
            MissingAssemblyNames: Array.Empty<string>(),
            IsMetadataReferenceClosureComplete: true);
        int graphBuildCount = 0;
        BuildStateRuntimeBuildProcessExecutor.GraphBuildOverride = _ =>
        {
            graphBuildCount++;
            return null;
        };

        ArchitecturePreparedBuildReceiptService service = new(new FakeRunnerSetupService(document, preparation));
        BuildStatePreflightResult published = service.Publish(new BuildStatePreparedCandidateRequest(
            Path.Combine(RepositoryRoot, "policy.arch.yml"),
            RequestedConfiguration: "Debug",
            RequestedTargetFramework: "net10.0",
            NoRestore: true));

        Assert.That(published.Blocked, Is.False,
            () => string.Join(" | ", published.Diagnostics.Select(d => $"{d.State}: {d.Evidence.Detail}")));
        Assert.That(published.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));
        Assert.That(graphBuildCount, Is.EqualTo(1), "publication must be authorized by one successful graph build");
        Assert.That(File.Exists(BuildReceiptStore.ReceiptPathFor(assemblyPath)), Is.True);

        BuildStateRuntimeBuildProcessExecutor.GraphBuildOverride = _ =>
            throw new AssertionException("ordinary fan-out must not invoke graph build");
        BuildStatePreflightResult ordinary = new BuildStatePreparationService().Prepare(
            new BuildStatePreflightRequest(
                RepositoryRoot,
                discovery,
                SingleAssemblyResolution(assemblyPath),
                BuildPreparationMode.Ordinary,
                NoRestore: true,
                RequestedConfiguration: "Debug",
                RequestedTargetFramework: "net10.0"));

        Assert.That(ordinary.Blocked, Is.False);
        Assert.That(ordinary.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));
        Assert.That(graphBuildCount, Is.EqualTo(1), "ordinary fan-out must not re-enter graph build");
    }

    [Test]
    public void Publish_WithCompletedSolutionBuildProof_PublishesWithoutEnteringGraphBuild()
    {
        string projectPath = CreateProjectFixture("ExternallyPreparedCandidateFixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("ExternallyPreparedCandidateFixture");
        string objDirectory = Path.Combine(Path.GetDirectoryName(projectPath)!, "obj");
        Directory.CreateDirectory(objDirectory);
        File.WriteAllText(Path.Combine(objDirectory, "project.assets.json"), "{\"targets\":{\"net10.0\":{}}}");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "ExternallyPreparedCandidateFixture") with
        {
            ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ExternallyPreparedCandidateFixture"] = assemblyPath,
            },
        };
        ArchitectureContractDocument document = new()
        {
            Version = 1,
            Name = "Externally prepared candidate test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Configuration = "Debug",
                TargetFramework = "net10.0",
            },
        };
        ArchitectureRunnerPreparation preparation = new(
            RepositoryRoot,
            PreprocessorSymbols: null,
            discovery,
            ResolveAssemblyOutputs: true,
            SelectedAssemblyArtifactPaths: new[] { assemblyPath },
            CapturedArtifactContentDigests: new Dictionary<string, string>(StringComparer.Ordinal),
            MissingAssemblyNames: Array.Empty<string>(),
            IsMetadataReferenceClosureComplete: true);
        string proofDirectory = Path.Combine(RepositoryRoot, "prepared-build-proof");
        Directory.CreateDirectory(proofDirectory);
        string nonce = "test-build-nonce";
        File.WriteAllText(
            Path.Combine(proofDirectory, "ExternallyPreparedCandidateFixture.net10.0.prepared-build-proof"),
            string.Join('|', Path.GetFullPath(projectPath), Path.GetFullPath(assemblyPath), "Debug", "net10.0", "", "", nonce));
        int graphBuildCount = 0;
        BuildStateRuntimeBuildProcessExecutor.GraphBuildOverride = _ =>
        {
            graphBuildCount++;
            throw new AssertionException("completed-build publication must not invoke graph build");
        };

        ArchitecturePreparedBuildReceiptService service = new(new FakeRunnerSetupService(document, preparation));
        BuildStatePreflightResult published = service.Publish(new BuildStatePreparedCandidateRequest(
            Path.Combine(RepositoryRoot, "policy.arch.yml"),
            RequestedConfiguration: "Debug",
            RequestedTargetFramework: "net10.0",
            NoRestore: true,
            PreparedBuildProofDirectory: proofDirectory,
            PreparedBuildProofNonce: nonce,
            BuildAlreadyCompleted: true));

        Assert.That(published.Blocked, Is.False,
            () => string.Join(" | ", published.Diagnostics.Select(d => $"{d.State}: {d.Evidence.Detail}")));
        Assert.That(published.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));
        Assert.That(graphBuildCount, Is.Zero);
        Assert.That(File.Exists(BuildReceiptStore.ReceiptPathFor(assemblyPath)), Is.True);
    }

    [Test]
    public void Publish_WithCompletedBuildClaimButWithoutProof_FailsClosedWithoutReceipt()
    {
        string projectPath = CreateProjectFixture("UnprovenPreparedCandidateFixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("UnprovenPreparedCandidateFixture");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "UnprovenPreparedCandidateFixture") with
        {
            ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UnprovenPreparedCandidateFixture"] = assemblyPath,
            },
        };
        ArchitectureContractDocument document = new() { Version = 1, Name = "Unproven candidate test" };
        ArchitectureRunnerPreparation preparation = new(
            RepositoryRoot,
            PreprocessorSymbols: null,
            discovery,
            ResolveAssemblyOutputs: true,
            SelectedAssemblyArtifactPaths: new[] { assemblyPath },
            CapturedArtifactContentDigests: new Dictionary<string, string>(StringComparer.Ordinal),
            MissingAssemblyNames: Array.Empty<string>(),
            IsMetadataReferenceClosureComplete: true);
        BuildStateRuntimeBuildProcessExecutor.GraphBuildOverride = _ =>
            throw new AssertionException("verification-only publication must not fall back to graph build");

        ArchitecturePreparedBuildReceiptService service = new(new FakeRunnerSetupService(document, preparation));
        BuildStatePreflightResult result = service.Publish(new BuildStatePreparedCandidateRequest(
            Path.Combine(RepositoryRoot, "policy.arch.yml"),
            PreparedBuildProofDirectory: Path.Combine(RepositoryRoot, "missing-proof"),
            PreparedBuildProofNonce: "missing-proof-nonce",
            BuildAlreadyCompleted: true));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.UnverifiableArtifact));
        Assert.That(File.Exists(BuildReceiptStore.ReceiptPathFor(assemblyPath)), Is.False);
    }

    [Test]
    public void Publish_DoesNotIssueReceiptWhenAuthoritativeBuildFails()
    {
        string projectPath = CreateProjectFixture("FailedPreparedCandidateFixture", "class C {}");
        string assemblyPath = CreateFakeAssemblyFile("FailedPreparedCandidateFixture");
        ProjectDiscoveryResult discovery = SingleProjectDiscovery(projectPath, "FailedPreparedCandidateFixture") with
        {
            ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["FailedPreparedCandidateFixture"] = assemblyPath,
            },
        };
        ArchitectureContractDocument document = new() { Version = 1, Name = "Failed candidate test" };
        ArchitectureRunnerPreparation preparation = new(
            RepositoryRoot,
            PreprocessorSymbols: null,
            discovery,
            ResolveAssemblyOutputs: true,
            SelectedAssemblyArtifactPaths: new[] { assemblyPath },
            CapturedArtifactContentDigests: new Dictionary<string, string>(StringComparer.Ordinal),
            MissingAssemblyNames: Array.Empty<string>(),
            IsMetadataReferenceClosureComplete: true);
        BuildStateRuntimeBuildProcessExecutor.GraphBuildOverride = _ => new BuildStatePreflightDiagnostic(
            "build-state-preflight",
            projectPath,
            BuildStatePreflightState.BuildFailed,
            new BuildStatePreflightEvidence(projectPath, "FailedPreparedCandidateFixture", Detail: "build failed"));
        ArchitecturePreparedBuildReceiptService service = new(new FakeRunnerSetupService(document, preparation));
        BuildStatePreflightResult result = service.Publish(new BuildStatePreparedCandidateRequest(
            Path.Combine(RepositoryRoot, "policy.arch.yml"),
            NoRestore: true));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.BuildFailed));
        Assert.That(File.Exists(BuildReceiptStore.ReceiptPathFor(assemblyPath)), Is.False);
    }

    [Test]
    public void Publish_RejectsCandidateWithoutAnArchitectureProjectGraph()
    {
        ArchitectureContractDocument document = new() { Version = 1, Name = "Empty candidate test" };
        ArchitectureRunnerPreparation preparation = new(
            RepositoryRoot,
            PreprocessorSymbols: null,
            ProjectDiscoveryResult.Empty,
            ResolveAssemblyOutputs: true,
            SelectedAssemblyArtifactPaths: Array.Empty<string>(),
            CapturedArtifactContentDigests: new Dictionary<string, string>(StringComparer.Ordinal),
            MissingAssemblyNames: Array.Empty<string>(),
            IsMetadataReferenceClosureComplete: true);
        ArchitecturePreparedBuildReceiptService service = new(new FakeRunnerSetupService(document, preparation));

        Assert.That(
            () => service.Publish(new BuildStatePreparedCandidateRequest(Path.Combine(RepositoryRoot, "policy.arch.yml"))),
            Throws.InvalidOperationException.With.Message.Contains("project graph selected"));
    }

    private sealed class FakeRunnerSetupService(
        ArchitectureContractDocument document,
        ArchitectureRunnerPreparation preparation) : IArchitectureRunnerSetupService
    {
        public ArchitectureContractDocument LoadDocument(string policyPath, string? baselinePath = null,
            ValidationTiming? timing = null) => document;

        public ArchitectureRunnerSetup BuildRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null) => throw new NotSupportedException();

        public ArchitectureRunnerSetup BuildRunnerForPostBuild(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null) => throw new NotSupportedException();

        public ArchitectureRunnerPreparation PrepareRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            string? mode = null,
            CancellationToken cancellationToken = default) => preparation;
    }
}
