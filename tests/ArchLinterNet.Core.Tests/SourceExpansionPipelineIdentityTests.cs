using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Drives the real validation pipeline rather than a directly constructed session, because contract
// selection is gated by the catalog's known-id set long before ArchitectureAnalysisSession's
// expansion-aware IsContractSelected overload is reached.
[TestFixture]
public sealed class SourceExpansionPipelineIdentityTests
{
    private const string CoreAssembly = "ArchLinterNet.Core";
    private const string CelAssembly = "ArchLinterNet.CEL";

    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-expansion-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static string Describe(ValidationOutcome outcome) =>
        $"violations=[{string.Join(" | ", outcome.Violations.Select(v => $"{v.ContractId}:{v.SourceType}:{v.ForbiddenNamespace}"))}] " +
        $"cycles={outcome.Cycles.Count} unmatched={outcome.UnmatchedIgnoredViolations.Count} " +
        $"consistency={outcome.PolicyConsistencyFindings.Count} coverage={outcome.CoverageFindings.Count} " +
        $"preflight={outcome.PreflightDiagnostics.Count}";

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_temporaryDirectory, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    // Uses the external-dependency family, which resolves entirely from scanned types: package and
    // framework families need project discovery, whose absence would fail the run for reasons
    // unrelated to contract selection.
    private string ExpandedExternalPolicy() => WritePolicy($"""
        version: 1
        name: Test

        layers:
          core:
            namespace: ArchLinterNet.Core
          cel:
            namespace: ArchLinterNet.CEL

        analysis:
          target_assemblies: [{CoreAssembly}, {CelAssembly}]
          policy_consistency: off

        source_sets:
          inner_layers:
            kind: layer
            members: [core, cel]

        external_dependencies:
          forbidden_vendor:
            namespace_prefixes: [Definitely.Not.Referenced]

        contracts:
          strict_external:
            - name: inner layers avoid vendor
              id: inner-no-vendor
              source_sets: [inner_layers]
              forbidden: [forbidden_vendor]
        """);

    [Test]
    public void Validate_SelectingAuthoredId_IsAcceptedByThePipeline()
    {
        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = ExpandedExternalPolicy(),
            Mode = "strict",
            ContractIds = new List<string> { "inner-no-vendor" }
        });

        Assert.That(outcome.Passed, Is.True, Describe(outcome));
    }

    [Test]
    public void Validate_SelectingDerivedInstanceId_IsAlsoAccepted()
    {
        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = ExpandedExternalPolicy(),
            Mode = "strict",
            ContractIds = new List<string> { "inner-no-vendor/core" }
        });

        Assert.That(outcome.Passed, Is.True);
    }

    [Test]
    public void Validate_SelectingUnknownId_StillFailsWithActionableDiagnostic()
    {
        InvalidOperationException exception = Assert.Catch<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = ExpandedExternalPolicy(),
                Mode = "strict",
                ContractIds = new List<string> { "no-such-contract" }
            }))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("Unknown contract IDs: no-such-contract"));
            Assert.That(exception.Message, Does.Contain("inner-no-vendor"));
        });
    }

    [Test]
    public void Catalog_OffersBothAuthoredAndDerivedIds()
    {
        ArchitectureContractDocument document =
            new ArchitecturePolicyDocumentLoader().Load(ExpandedExternalPolicy());
        HashSet<string> available = Execution.ArchitectureContractCatalog.Build(document).AvailableContractIds("strict");

        Assert.Multiple(() =>
        {
            Assert.That(available, Does.Contain("inner-no-vendor"));
            Assert.That(available, Does.Contain("inner-no-vendor/core"));
            Assert.That(available, Does.Contain("inner-no-vendor/cel"));
        });
    }

    [Test]
    public void InstanceIdsFor_SpansEveryFamilyAndModeSharingOneAuthoredId()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            layers:
              core:
                namespace: ArchLinterNet.Core

            analysis:
              target_assemblies: [{CoreAssembly}, {CelAssembly}]

            source_sets:
              scanned_assemblies:
                globs: ["ArchLinterNet.*"]
              core_layers:
                kind: layer
                members: [core]

            packages:
              forbidden_infra:
                package_ids: [Definitely.Not.Referenced]

            external_dependencies:
              forbidden_vendor:
                namespace_prefixes: [Definitely.Not.Referenced]

            contracts:
              strict_package_dependency:
                - name: assemblies avoid infrastructure packages
                  id: shared-id
                  source_sets: [scanned_assemblies]
                  forbidden: [forbidden_infra]
              audit_external:
                - name: core avoids vendor
                  id: shared-id
                  source_sets: [core_layers]
                  forbidden: [forbidden_vendor]
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        IReadOnlyList<string> instanceIds = document.SourceExpansion.InstanceIdsFor("shared-id");

        // The same authored id is legal in two different contract type/mode groups; resolving only
        // the first would silently cover part of what the author referenced.
        Assert.Multiple(() =>
        {
            Assert.That(instanceIds, Does.Contain("shared-id/archlinternet-core"));
            Assert.That(instanceIds, Does.Contain("shared-id/archlinternet-cel"));
            Assert.That(instanceIds, Does.Contain("shared-id/core"));
            Assert.That(instanceIds, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void RuleInputCoverage_AuthoredIdWithDanglingLayer_DefersInsteadOfConfigurationError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            layers:
              core:
                namespace: ArchLinterNet.Core
              cel:
                namespace: ArchLinterNet.CEL

            analysis:
              target_assemblies: [{CoreAssembly}]
              coverage: warn

            source_sets:
              inner_layers:
                kind: layer
                members: [core, cel]

            external_dependencies:
              forbidden_vendor:
                namespace_prefixes: [Definitely.Not.Referenced]

            contracts:
              strict_external:
                - name: inner layers avoid vendor
                  id: inner-no-vendor
                  source_sets: [inner_layers]
                  forbidden: [forbidden_vendor, missing_layer_group]
              strict_coverage:
                - name: rule input coverage
                  id: rule-input-coverage
                  scope: rule_input
                  contract_ids: [inner-no-vendor]
                  reason: Flag rules whose inputs stop matching any code.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        // The dangling external group is handed to rule-input coverage rather than crashing the run.
        Assert.That(outcome.CoverageSummaries.Any(summary => summary.ContractId == "rule-input-coverage"), Is.True);
    }

    [Test]
    public void RuleInputCoverage_OptionalInputOnAuthoredId_IsValidatedAgainstItsInstances()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test

            layers:
              core:
                namespace: ArchLinterNet.Core
              future_slice:
                namespace: ArchLinterNet.FutureSlice

            analysis:
              target_assemblies: [{CoreAssembly}]

            source_sets:
              inner_layers:
                kind: layer
                members: [core, future_slice]

            external_dependencies:
              forbidden_vendor:
                namespace_prefixes: [Definitely.Not.Referenced]

            contracts:
              strict_external:
                - name: inner layers avoid vendor
                  id: inner-no-vendor
                  source_sets: [inner_layers]
                  forbidden: [forbidden_vendor]
              strict_coverage:
                - name: rule input coverage
                  id: rule-input-coverage
                  scope: rule_input
                  contract_ids: [inner-no-vendor]
                  optional_inputs:
                    - contract_id: inner-no-vendor
                      input: source
                      layer: future_slice
                      reason: The future slice is declared before it is implemented.
                  reason: Flag rules whose inputs stop matching any code.
            """);

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        ArchitectureCoverageSummary summary =
            outcome.CoverageSummaries.Single(candidate => candidate.ContractId == "rule-input-coverage");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Passed, Is.True, Describe(outcome));
            Assert.That(summary.Counts.OptionalEmpty, Is.EqualTo(1));
        });
    }

    [Test]
    public void BaselineSelection_ByAuthoredId_ResolvesEveryInstanceEntry()
    {
        ArchitectureContractDocument document =
            new ArchitecturePolicyDocumentLoader().Load(ExpandedExternalPolicy());

        ArchitectureBaselineDocument baseline = new() { Version = 2 };
        baseline.Baseline.StrictExternal.Add(new ArchitectureBaselineContractEntry
        {
            Id = "inner-no-vendor/core",
            IgnoredViolations =
            {
                new ArchitectureBaselineIgnoredViolation
                {
                    SourceType = CoreAssembly,
                    ForbiddenReference = "Definitely.Not.Referenced",
                    Reason = "reviewed debt"
                }
            }
        });

        ArchitectureBaselineComparisonResult selectedByAuthoredId = ArchitectureBaselineComparer.Compare(
            document, baseline, Array.Empty<ArchitectureBaselineCandidate>(), "strict",
            new[] { "inner-no-vendor" });
        ArchitectureBaselineComparisonResult selectedByUnrelatedId = ArchitectureBaselineComparer.Compare(
            document, baseline, Array.Empty<ArchitectureBaselineCandidate>(), "strict",
            new[] { "some-other-contract" });

        Assert.Multiple(() =>
        {
            Assert.That(selectedByAuthoredId.OutOfScope, Is.Empty);
            Assert.That(selectedByUnrelatedId.OutOfScope, Is.Not.Empty);
        });
    }
}
