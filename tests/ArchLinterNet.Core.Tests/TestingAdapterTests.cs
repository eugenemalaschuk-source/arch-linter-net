using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class TestingAdapterTests
{
    private static readonly string[] _rulesFragmentPaths = { "architecture/rules.yml" };
    private static readonly string[] _selfForbiddenIds = { "self-forbidden" };
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
    public void ValidateStrict_CleanPolicy_Passes()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Clean Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void ShouldPass_WhenFailed_ThrowsInvalidOperationException()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Clean Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.DoesNotThrow(() => result.ShouldPass());
    }

    [Test]
    public void FromRepositoryRoot_LoadsPolicyFromArchitectureDir()
    {
        string repoDir = Path.Combine(_tempDir, "myrepo");
        string archDir = Path.Combine(repoDir, "architecture");
        Directory.CreateDirectory(archDir);

        string policyPath = Path.Combine(archDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, @"
version: 1
name: Root Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
");

        var result = ArchitectureAssertions.FromRepositoryRoot(repoDir).ValidateStrict();

        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void UnknownConditionSet_ThrowsInvalidOperation()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
  condition_sets:
    runtime: []
contracts:
  strict: []
  strict_layers: []
  strict_allow_only: []
  strict_cycles: []
  strict_method_body: []
  strict_asmdef: []
  strict_independence: []
");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureAssertions
                .FromPolicy(contractPath)
                .WithConditionSet("nonexistent")
                .ValidateStrict());

        Assert.That(ex!.Message, Does.Contain("Unknown condition set"));
        Assert.That(ex.Message, Does.Contain("nonexistent"));
        Assert.That(ex.Message, Does.Contain("runtime"));
    }

    [Test]
    public void ValidateStrict_IndependenceConflict_Fails()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Independence Conflict Test
layers:
  core:
    namespace: ArchLinterNet.Core
  contracts:
    namespace: ArchLinterNet.Core.Contracts
analysis:
  target_assemblies:
    - ArchLinterNet.Core
  policy_consistency: error
contracts:
  strict_independence:
    - name: core-contracts-independent
      layers: [core, contracts]
  strict_allow_only:
    - name: core-allows-contracts
      source: core
      allowed: [contracts]
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Passed, Is.False);
    }

    [Test]
    public void ValidateStrict_ImportedContract_ExposesTheSharedFragmentProvenance()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");
        File.WriteAllText(contractPath, """
            version: 1
            name: Testing provenance
            imports: [rules.yml]
            layers:
              core:
                namespace: ArchLinterNet.Core
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts: {}
            """);
        File.WriteAllText(Path.Combine(contractDir, "rules.yml"), """
            contracts:
              strict:
                - id: self-forbidden
                  name: core-must-not-depend-on-itself
                  source: core
                  forbidden: [core]
            """);

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Violations, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(result.Violations.All(violation => violation.PolicyLocation is not null), Is.True);
            Assert.That(result.Violations.Select(violation => violation.PolicyLocation!.SourcePath).Distinct(),
                Is.EqualTo(_rulesFragmentPaths));
            Assert.That(result.Violations.Select(violation => violation.PolicyLocation!.ContractId).Distinct(),
                Is.EqualTo(_selfForbiddenIds));
        });
    }

    [Test]
    public void ValidateStrict_ImportedCycleContract_ExposesFragmentProvenanceInTestingOutput()
    {
        string assemblyName = typeof(HandlerRegistryCycleFixtures.LayerA.ServiceA).Assembly.GetName().Name!;
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");
        File.WriteAllText(contractPath, $@"
version: 1
name: Testing cycle provenance
imports: [rules.yml]
layers:
  layerA:
    namespace: HandlerRegistryCycleFixtures.LayerA
  layerB:
    namespace: HandlerRegistryCycleFixtures.LayerB
analysis:
  target_assemblies: [{assemblyName}]
contracts: {{}}
");
        File.WriteAllText(Path.Combine(contractDir, "rules.yml"), """
            contracts:
              strict_cycles:
                - id: cycle-check
                  name: imported-cycle
                  layers: [layerA, layerB]
            """);

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.CycleFindings, Is.Not.Empty);
        Assert.That(result.CycleFindings.Select(finding => finding.PolicyLocation!.SourcePath).Distinct(),
            Is.EqualTo(_rulesFragmentPaths));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => result.ShouldPass())!;
        Assert.That(exception.Message, Does.Contain("policy: architecture/rules.yml:contracts.strict_cycles[0]"));
    }

    [Test]
    public void ShouldPass_PolicyConsistencyOnlyFailure_ThrowsWithCheckKindAndReason()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Duplicate Id Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
  policy_consistency: error
contracts:
  strict:
    - id: dup-id
      name: core-no-forbidden
      source: core
      forbidden: []
  audit:
    - id: dup-id
      name: contracts-no-forbidden
      source: core
      forbidden: []
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Passed, Is.False);
        Assert.That(result.Violations, Is.Empty);
        Assert.That(result.Cycles, Is.Empty);
        Assert.That(result.PolicyConsistencyFindings, Is.Not.Empty);

        var ex = Assert.Throws<InvalidOperationException>(() => result.ShouldPass());

        Assert.That(ex!.Message, Does.Contain("duplicate-id"));
        Assert.That(ex.Message, Does.Contain("core-no-forbidden"));
        Assert.That(ex.Message, Does.Contain("contracts-no-forbidden"));
    }

    private string WriteSelfForbiddenPolicy()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Builder Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict:
    - id: self-forbidden
      name: core-must-not-depend-on-itself
      source: core
      forbidden: [core]
    - id: harmless
      name: harmless-rule
      source: core
      forbidden: []
");
        return contractPath;
    }

    [Test]
    public void WithContracts_OnlySelectedContractRuns()
    {
        string contractPath = WriteSelfForbiddenPolicy();

        var result = ArchitectureAssertions.FromPolicy(contractPath)
            .WithContracts("harmless")
            .ValidateStrict();

        Assert.That(result.Passed, Is.True);
        Assert.That(result.Violations, Is.Empty);
    }

    private string WriteSelfForbiddenAuditPolicy()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Audit Builder Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  audit:
    - id: self-forbidden
      name: core-must-not-depend-on-itself
      source: core
      forbidden: [core]
    - id: harmless
      name: harmless-rule
      source: core
      forbidden: []
");
        return contractPath;
    }

    [Test]
    public void ValidateAudit_WithContracts_OnlySelectedContractRuns()
    {
        string contractPath = WriteSelfForbiddenAuditPolicy();

        var withHarmlessOnly = ArchitectureAssertions.FromPolicy(contractPath)
            .WithContracts("harmless")
            .ValidateAudit();

        Assert.That(withHarmlessOnly.Passed, Is.True);
        Assert.That(withHarmlessOnly.Violations, Is.Empty);

        var unfiltered = ArchitectureAssertions.FromPolicy(contractPath).ValidateAudit();

        Assert.That(unfiltered.Passed, Is.False,
            "Without a contract filter, the self-forbidden audit contract should still report violations");
        Assert.That(unfiltered.Violations, Is.Not.Empty);
    }

    [Test]
    public void WithBaseline_SuppressesKnownViolation()
    {
        string contractPath = WriteSelfForbiddenPolicy();

        var before = ArchitectureAssertions.FromPolicy(contractPath)
            .WithContracts("self-forbidden")
            .ValidateStrict();

        Assert.That(before.Violations, Is.Not.Empty, "Expected at least one baseline violation for test validity");
        var known = before.Violations.First();
        string forbiddenRef = known.ForbiddenReferences.First();

        string baselinePath = Path.Combine(_tempDir, "baseline.yml");
        File.WriteAllText(baselinePath, $@"
version: 1
baseline:
  strict:
    - id: self-forbidden
      ignored_violations:
        - source_type: {known.SourceType}
          forbidden_reference: {forbiddenRef}
          reason: known debt
");

        var after = ArchitectureAssertions.FromPolicy(contractPath)
            .WithContracts("self-forbidden")
            .WithBaseline(baselinePath)
            .ValidateStrict();

        Assert.That(after.Violations,
            Has.None.Matches<ArchLinterNet.Core.Model.ArchitectureViolation>(v =>
                v.SourceType == known.SourceType && v.ForbiddenReferences.Contains(forbiddenRef)),
            "Baselined violation should be suppressed");
    }

    private string WriteUnmatchedIgnorePolicy()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Unmatched Ignore Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
  unmatched_ignored_violations: error
contracts:
  strict:
    - name: harmless-with-stale-ignore
      source: core
      forbidden: []
      ignored_violations:
        - source_type: Does.Not.Exist
          forbidden_reference: Also.Does.Not.Exist
          reason: stale
");
        return contractPath;
    }

    [Test]
    public void WithUnmatchedIgnoredViolationsPolicy_NotCalled_PassesByDefault()
    {
        string contractPath = WriteUnmatchedIgnorePolicy();

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Passed, Is.True);
        Assert.That(result.UnmatchedIgnoredViolations, Is.Empty);
    }

    [Test]
    public void WithUnmatchedIgnoredViolationsPolicy_Called_Fails()
    {
        string contractPath = WriteUnmatchedIgnorePolicy();

        var result = ArchitectureAssertions.FromPolicy(contractPath)
            .WithUnmatchedIgnoredViolationsPolicy()
            .ValidateStrict();

        Assert.That(result.Passed, Is.False);
        Assert.That(result.UnmatchedIgnoredViolations, Is.Not.Empty);
    }

    [Test]
    public void ShouldPass_UnmatchedIgnoredDetail_IncludedInMessage()
    {
        string contractPath = WriteUnmatchedIgnorePolicy();

        var result = ArchitectureAssertions.FromPolicy(contractPath)
            .WithUnmatchedIgnoredViolationsPolicy()
            .ValidateStrict();

        var ex = Assert.Throws<InvalidOperationException>(() => result.ShouldPass());

        Assert.That(ex!.Message, Does.Contain("Unmatched ignored violations"));
        Assert.That(ex.Message, Does.Contain("Does.Not.Exist"));
    }

    [Test]
    public void WithTimings_PopulatesTiming()
    {
        string contractPath = WriteSelfForbiddenPolicy();

        var result = ArchitectureAssertions.FromPolicy(contractPath)
            .WithContracts("harmless")
            .WithTimings()
            .ValidateStrict();

        Assert.That(result.Timing, Is.Not.Null);

        using var writer = new StringWriter();
        result.Timing!.WriteReport(writer);

        Assert.That(writer.ToString(), Does.Contain("total"));
    }

    [Test]
    public void ValidateStrict_WithoutTimings_TimingIsNull()
    {
        string contractPath = WriteSelfForbiddenPolicy();

        var result = ArchitectureAssertions.FromPolicy(contractPath)
            .WithContracts("harmless")
            .ValidateStrict();

        Assert.That(result.Timing, Is.Null);
    }

    [Test]
    public void ValidateStrict_CoverageContract_SurfacesFindingsAndSummaries()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Coverage Test
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict_coverage:
    - id: namespace-coverage
      name: namespace-coverage
      scope: namespace
      roots:
        - namespace: ArchLinterNet.Core
      reason: All namespaces must be mapped or excluded.
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateStrict();

        Assert.That(result.Passed, Is.False);
        Assert.That(result.CoverageFindings, Is.Not.Empty);
        Assert.That(result.CoverageSummaries, Is.Not.Empty);

        var ex = Assert.Throws<InvalidOperationException>(() => result.ShouldPass());
        Assert.That(ex!.Message, Does.Contain("Coverage findings"));
    }
}
