using ArchLinterNet.Core;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Discovery.Abstractions;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Validation;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyCheckTests
{
    private sealed class RecordingProjectDiscoveryService : IArchitectureProjectDiscoveryService
    {
        public bool WasCalled { get; private set; }

        public ProjectDiscoveryResult ResolveAndApply(
            ArchitectureContractDocument document,
            string repositoryRoot,
            bool resolveAssemblyOutputs,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("Policy check must not evaluate projects.");
        }
    }

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public void CheckPolicy_SelectorOnlyLayer_ReturnsDeferredCheck()
    {
        string policyPath = Path.Combine(_tempDir, "selector-only.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Selector Only
            layers:
              domain:
                namespace: Example.Domain
                selector:
                  role: DomainLayer
            analysis:
              target_assemblies: []
            contracts: {}
            """);

        PolicyCheckOutcome outcome = ArchitectureValidator.CheckPolicy(policyPath);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.IsValid, Is.True);
            Assert.That(outcome.DeferredChecks.Single().Kind, Is.EqualTo("layer-selector"));
            Assert.That(outcome.DeferredChecks.Single().PolicyLocations.Single().YamlPath,
                Is.EqualTo("layers.domain"));
        });
    }

    [Test]
    public void CheckPolicy_UnsafeApiSnapshot_UsesFailingContractProvenance()
    {
        string policyPath = Path.Combine(_tempDir, "snapshot-provenance.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Snapshot Provenance
            layers: {}
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_public_api_surface:
                - id: valid-contract
                  name: valid-contract
                  assemblies: [ArchLinterNet.Core]
                  api_comparison: additions_only
                  reason: A valid contract before the invalid one.
                - id: unsafe-contract
                  name: unsafe-contract
                  assemblies: [ArchLinterNet.Core]
                  api_snapshot: ../outside.txt
                  api_comparison: additions_only
                  reason: The failing contract.
            """);

        PolicyCheckOutcome outcome = ArchitectureValidator.CheckPolicy(policyPath);

        Assert.That(outcome.Failure!.Diagnostic!.Location!.ContractId, Is.EqualTo("unsafe-contract"));
    }

    [Test]
    public void CheckPolicy_DoesNotEvaluateProjectsOrLoadTargetAssemblies()
    {
        string policyPath = Path.Combine(_tempDir, "no-process-evaluation.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: No Process Evaluation
            layers: {}
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts: {}
            """);
        var projectDiscovery = new RecordingProjectDiscoveryService();
        var assemblyLoader = new FakeArchitectureAssemblyLoader(Array.Empty<System.Reflection.Assembly>());
        ArchitectureEngine engine = new ArchitectureEngineBuilder()
            .AddArchLinterNetCore()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IArchitectureProjectDiscoveryService>(projectDiscovery);
                services.AddSingleton<IArchitectureAssemblyLoader>(assemblyLoader);
            })
            .Build();

        PolicyCheckOutcome outcome = engine.CheckPolicy(policyPath);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.IsValid, Is.True);
            Assert.That(projectDiscovery.WasCalled, Is.False);
            Assert.That(assemblyLoader.LoadWasCalled, Is.False);
            Assert.That(assemblyLoader.LoadFromWasCalled, Is.False);
        });
    }

    [Test]
    public void CheckPolicy_IncompleteStructuredWaiver_ReturnsFailClosedLifecycleValidation()
    {
        string policyPath = Path.Combine(_tempDir, "incomplete-waiver.yml");
        File.WriteAllText(policyPath, """
            version: 2
            name: Incomplete waiver
            layers: {}
            analysis:
              target_assemblies: []
            contracts:
              strict:
                - id: boundary
                  name: boundary
                  source: app
                  forbidden: [infrastructure]
                  ignored_violations:
                    - id: ARCH-IGN-001
                      source_type: App.Legacy
                      forbidden_reference: Infrastructure.Db
                      target:
                        fingerprint: sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                      reason: Temporary migration
                      issue: ARCH-231
                      introduced: 2026-08-01
                      expires: 2026-10-01
            """);

        PolicyCheckOutcome outcome = ArchitectureValidator.CheckPolicy(policyPath);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.IsValid, Is.False);
            Assert.That(outcome.Failure!.Message, Does.Contain("requires id, target.fingerprint"));
            Assert.That(outcome.Failure.Diagnostic!.Location!.ContractId, Is.EqualTo("boundary"));
        });
    }

    [Test]
    public void CheckPolicy_UppercaseWaiverFingerprint_IsRejectedBySchema()
    {
        string policyPath = Path.Combine(_tempDir, "uppercase-waiver.yml");
        File.WriteAllText(policyPath, """
            version: 2
            name: Uppercase waiver
            layers: {}
            analysis:
              target_assemblies: []
            contracts:
              strict:
                - id: boundary
                  name: boundary
                  source: app
                  forbidden: [infrastructure]
                  ignored_violations:
                    - id: ARCH-IGN-001
                      source_type: App.Legacy
                      forbidden_reference: Infrastructure.Db
                      target:
                        fingerprint: sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                      reason: Temporary migration
                      owner: architecture-team
                      issue: ARCH-231
                      introduced: 2026-08-01
                      expires: 2026-10-01
            """);

        PolicyCheckOutcome outcome = ArchitectureValidator.CheckPolicy(policyPath);

        Assert.That(outcome.IsValid, Is.False);
    }

    [Test]
    public void PolicyDocumentLoader_PublicContractPreservesSinglePathLoadMethod()
    {
        System.Reflection.MethodInfo? method = typeof(IArchitecturePolicyDocumentLoader)
            .GetMethod(nameof(IArchitecturePolicyDocumentLoader.Load), [typeof(string)]);

        Assert.That(method?.GetParameters(), Has.Length.EqualTo(1));
    }
}
