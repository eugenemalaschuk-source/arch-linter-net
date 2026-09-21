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
    public void Publish_VerifiesPreviousBuildProof_AndOrdinaryFanoutDoesNotBuildAgain()
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
        const string BuildProofNonce = "prepared-candidate-proof";
        WriteBuildProof(assemblyPath, projectPath, BuildProofNonce);
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
            throw new AssertionException("prepared receipt publication must not invoke graph build");
        };

        ArchitecturePreparedBuildReceiptService service = new(new FakeRunnerSetupService(document, preparation));
        BuildStatePreflightResult published = service.Publish(new BuildStatePreparedCandidateRequest(
            Path.Combine(RepositoryRoot, "policy.arch.yml"),
            RequestedConfiguration: "Debug",
            RequestedTargetFramework: "net10.0",
            NoRestore: true,
            BuildProofNonce: BuildProofNonce));

        Assert.That(published.Blocked, Is.False,
            () => string.Join(" | ", published.Diagnostics.Select(d => $"{d.State}: {d.Evidence.Detail}")));
        Assert.That(published.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.Current));
        Assert.That(graphBuildCount, Is.EqualTo(0));
        Assert.That(File.Exists(BuildReceiptStore.ReceiptPathFor(assemblyPath)), Is.True);

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
        Assert.That(graphBuildCount, Is.EqualTo(0), "ordinary fan-out must not re-enter graph build");
    }

    [Test]
    public void Publish_DoesNotIssueReceiptWithoutAuthoritativeBuildProof()
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
        ArchitecturePreparedBuildReceiptService service = new(new FakeRunnerSetupService(document, preparation));
        BuildStatePreflightResult result = service.Publish(new BuildStatePreparedCandidateRequest(
            Path.Combine(RepositoryRoot, "policy.arch.yml"),
            NoRestore: true,
            BuildProofNonce: "missing-proof"));

        Assert.That(result.Blocked, Is.True);
        Assert.That(result.Diagnostics.Single().State, Is.EqualTo(BuildStatePreflightState.UnverifiableArtifact));
        Assert.That(File.Exists(BuildReceiptStore.ReceiptPathFor(assemblyPath)), Is.False);
    }

    private static void WriteBuildProof(string assemblyPath, string projectPath, string nonce)
    {
        File.WriteAllLines(
            Path.Combine(Path.GetDirectoryName(assemblyPath)!, ".arch-linter-net-build-proof"),
            new[]
            {
                "schema=architecture-build-proof/v1",
                $"nonce={nonce}",
                $"project={Path.GetFullPath(projectPath)}",
                $"target={Path.GetFullPath(assemblyPath)}",
            });
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
