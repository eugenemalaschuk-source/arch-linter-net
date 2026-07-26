using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// End-to-end coverage of the public-api operations through the composed ArchitectureEngine, against
// a real policy file and the real test assembly — the path the CLI actually takes.
[TestFixture]
public sealed class PublicApiEngineEndToEndTests
{
    private const string ContractId = "engine-surface";
    private const string CleanDeclaredTypeName = "PublicApiSurfaceContractTestFixtures.CleanDeclaredType";

    private string _repositoryRoot = null!;
    private string _policyPath = null!;

    private static string AssemblyName => typeof(PublicApiEngineEndToEndTests).Assembly.GetName().Name!;

    [SetUp]
    public void SetUp()
    {
        _repositoryRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-public-api-engine-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_repositoryRoot, "architecture", "api"));
        _policyPath = Path.Combine(_repositoryRoot, "architecture", "dependencies.arch.yml");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repositoryRoot))
        {
            Directory.Delete(_repositoryRoot, true);
        }
    }

    private void WritePolicy(string? apiSnapshot = null)
    {
        string snapshotLine = apiSnapshot == null ? string.Empty : $"\n      api_snapshot: {apiSnapshot}";
        File.WriteAllText(_policyPath, $"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_public_api_surface:
                - id: {ContractId}
                  name: {ContractId}
                  assemblies: [{AssemblyName}]{snapshotLine}
                  reason: Engine end-to-end coverage for the public-api workflow.
            """);
    }

    private static ArchitectureEngine CreateEngine()
    {
        return new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
    }

    [Test]
    public void CapturePublicApi_ThroughEngine_ProducesAParsableSnapshot()
    {
        WritePolicy();
        using ArchitectureEngine engine = CreateEngine();

        PublicApiCaptureOutcome outcome = engine.CapturePublicApi(new PublicApiCaptureRequest
        {
            PolicyPath = _policyPath,
            ContractId = ContractId,
            OutputPath = "architecture/api/surface.txt",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True, outcome.Error);
            Assert.That(outcome.EntryCount, Is.GreaterThan(0));
            Assert.That(
                PublicApiSnapshotFormat.Parse(outcome.Snapshot!, "captured").Entries.Select(e => e.Signature),
                Has.Some.EqualTo($"class {CleanDeclaredTypeName} [sealed]"));
        });
    }

    // The bootstrap the whole workflow advertises: a policy already declaring `api_snapshot` must be
    // loadable by the very capture that creates that file for the first time.
    [Test]
    public void CapturePublicApi_ThroughEngine_WorksWhenTheDeclaredSnapshotDoesNotExistYet()
    {
        WritePolicy("architecture/api/surface.txt");
        using ArchitectureEngine engine = CreateEngine();

        PublicApiCaptureOutcome outcome = engine.CapturePublicApi(new PublicApiCaptureRequest
        {
            PolicyPath = _policyPath,
            ContractId = ContractId,
            OutputPath = "architecture/api/surface.txt",
        });

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(_repositoryRoot, "architecture", "api", "surface.txt")), Is.False);
            Assert.That(outcome.Succeeded, Is.True, outcome.Error);
            Assert.That(outcome.ResolvedOutputPath, Is.EqualTo(
                Path.GetFullPath(Path.Combine(_repositoryRoot, "architecture", "api", "surface.txt"))));
        });
    }

    [Test]
    public void ValidateStrict_MissingDeclaredSnapshot_StillFailsLoudly()
    {
        WritePolicy("architecture/api/surface.txt");

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = _policyPath,
            Mode = "strict",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Passed, Is.False);
            Assert.That(
                outcome.Violations.Select(violation => (violation.Payload as PublicApiSurfacePayload)?.ApiDeltaKind),
                Does.Contain("snapshot-unusable"));
        });
    }

    [Test]
    public void CapturePublicApi_ThroughEngine_RefusesThePolicyFileAsDestination()
    {
        WritePolicy();
        using ArchitectureEngine engine = CreateEngine();

        PublicApiCaptureOutcome outcome = engine.CapturePublicApi(new PublicApiCaptureRequest
        {
            PolicyPath = _policyPath,
            ContractId = ContractId,
            OutputPath = "architecture/dependencies.arch.yml",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("Refusing to use the policy file"));
            Assert.That(outcome.FailureKind, Is.EqualTo(PublicApiFailureKind.InvalidInput));
        });
    }

    [Test]
    public void CapturePublicApi_ThroughEngine_RefusesAnEscapingDestination()
    {
        WritePolicy();
        using ArchitectureEngine engine = CreateEngine();

        PublicApiCaptureOutcome outcome = engine.CapturePublicApi(new PublicApiCaptureRequest
        {
            PolicyPath = _policyPath,
            ContractId = ContractId,
            OutputPath = "../../outside.txt",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Error, Does.Contain("outside the policy boundary"));
        });
    }

    [Test]
    public void DiffPublicApi_ThroughEngine_ReportsInSyncAfterCapture()
    {
        WritePolicy();
        using ArchitectureEngine engine = CreateEngine();

        PublicApiCaptureOutcome captured = engine.CapturePublicApi(new PublicApiCaptureRequest
        {
            PolicyPath = _policyPath,
            ContractId = ContractId,
            OutputPath = "architecture/api/surface.txt",
        });
        File.WriteAllText(Path.Combine(_repositoryRoot, "architecture", "api", "surface.txt"), captured.Snapshot!);

        PublicApiDiffOutcome outcome = engine.DiffPublicApi(new PublicApiDiffRequest
        {
            PolicyPath = _policyPath,
            ContractId = ContractId,
            SnapshotPath = "architecture/api/surface.txt",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True, outcome.Error);
            Assert.That(outcome.InSync, Is.True);
        });
    }

    [Test]
    public void UpdatePublicApi_ThroughEngine_ReturnsSnapshotForASnapshotBackedContract()
    {
        WritePolicy();
        using ArchitectureEngine engine = CreateEngine();

        PublicApiCaptureOutcome captured = engine.CapturePublicApi(new PublicApiCaptureRequest
        {
            PolicyPath = _policyPath,
            ContractId = ContractId,
            OutputPath = "architecture/api/surface.txt",
        });
        File.WriteAllText(Path.Combine(_repositoryRoot, "architecture", "api", "surface.txt"), captured.Snapshot!);
        WritePolicy("architecture/api/surface.txt");

        PublicApiUpdateOutcome outcome = engine.UpdatePublicApi(new PublicApiUpdateRequest
        {
            PolicyPath = _policyPath,
            ContractId = ContractId,
            SnapshotPath = "architecture/api/surface.txt",
            DryRun = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True, outcome.Error);
            Assert.That(outcome.Snapshot, Is.EqualTo(captured.Snapshot));
            Assert.That(outcome.Delta.HasChanges, Is.False);
        });
    }

    [Test]
    public void MigratePublicApi_ThroughEngine_RefusesDriftedInlineList()
    {
        File.WriteAllText(_policyPath, $"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_public_api_surface:
                - id: {ContractId}
                  name: {ContractId}
                  assemblies: [{AssemblyName}]
                  declared_api:
                    - "class PublicApiSurfaceContractTestFixtures.NeverExisted"
                  reason: Engine end-to-end coverage for migration drift refusal.
            """);
        using ArchitectureEngine engine = CreateEngine();

        PublicApiMigrateOutcome outcome = engine.MigratePublicApi(new PublicApiMigrateRequest
        {
            PolicyPath = _policyPath,
            ContractId = ContractId,
            OutputPath = "architecture/api/surface.txt",
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.Snapshot, Is.Null);
            Assert.That(
                outcome.StaleDeclarations,
                Does.Contain("class PublicApiSurfaceContractTestFixtures.NeverExisted"));
        });
    }

    [Test]
    public void ValidateStrict_ExactModeSnapshotContract_ReportsRemovalEndToEnd()
    {
        File.WriteAllText(Path.Combine(_repositoryRoot, "architecture", "api", "surface.txt"),
            PublicApiSnapshotFormat.Serialize(new PublicApiSnapshotDocument(
                PublicApiSnapshotFormat.CurrentVersion,
                ContractId,
                new[]
                {
                    new PublicApiSnapshotEntry(AssemblyName, "class PublicApiSurfaceContractTestFixtures.NeverExisted"),
                })));

        File.WriteAllText(_policyPath, $"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [{AssemblyName}]

            contracts:
              strict_public_api_surface:
                - id: {ContractId}
                  name: {ContractId}
                  assemblies: [{AssemblyName}]
                  api_snapshot: architecture/api/surface.txt
                  api_comparison: exact
                  reason: Exact-mode removal must surface through the validation service.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = _policyPath,
            Mode = "strict",
        });

        Assert.That(
            outcome.Violations
                .Select(violation => violation.Payload as PublicApiSurfacePayload)
                .Any(payload => payload?.ApiDeltaKind == "removed"
                    && payload.UndeclaredApiSignature == "class PublicApiSurfaceContractTestFixtures.NeverExisted"),
            Is.True);
    }
}
