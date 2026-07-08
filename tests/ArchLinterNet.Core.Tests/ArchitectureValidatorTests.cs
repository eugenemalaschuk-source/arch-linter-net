using ArchLinterNet.Core;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureValidatorTests
{
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
    public void Validate_PassesCleanPolicy()
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

        var validator = new ArchitectureValidator();
        bool result = validator.Validate(contractPath, out var violations, out var cycles);

        Assert.That(result, Is.True);
        Assert.That(violations, Is.Empty);
        Assert.That(cycles, Is.Empty);
    }

    [Test]
    public void Validate_ThreeArgOverload_ReturnsViolationsAndCycles()
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

        var validator = new ArchitectureValidator();
        bool result = validator.Validate(contractPath);

        Assert.That(result, Is.True);
    }

    [Test]
    public void Validate_FailsPolicyWithViolatedContract()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Failing Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict:
    - name: core-must-not-depend-on-itself
      source: core
      forbidden: [core]
");

        var validator = new ArchitectureValidator();
        bool result = validator.Validate(contractPath, out var violations, out var cycles);

        Assert.That(result, Is.False);
        Assert.That(violations, Is.Not.Empty);
        Assert.That(cycles, Is.Empty);
    }

    [Test]
    public void Validate_FailsPolicyWithViolatedAsmdefContract()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        string assetsDir = Path.Combine(_tempDir, "Assets");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, "MyAssembly.asmdef"), @"
{
  ""name"": ""MyAssembly"",
  ""references"": [ ""Forbidden.Editor"" ]
}
");

        File.WriteAllText(contractPath, @"
version: 1
name: Asmdef Failing Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict_asmdef:
    - name: my-assembly-must-not-reference-forbidden
      source_assemblies: [MyAssembly]
      forbidden_asmdef_prefixes: [Forbidden]
");

        var validator = new ArchitectureValidator();
        bool result = validator.Validate(contractPath, out var violations, out var cycles);

        Assert.That(result, Is.False);
        Assert.That(violations, Has.Some.Matches<ArchitectureViolation>(v => v.SourceType == "MyAssembly"));
        Assert.That(cycles, Is.Empty);
    }

    [Test]
    public void Validate_AllowForbidConflict_FailsAndSurfacesInViolations()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Policy Consistency Test
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
  strict:
    - name: core-forbids-contracts
      source: core
      forbidden: [contracts]
  strict_allow_only:
    - name: core-allows-contracts
      source: core
      allowed: [contracts]
");

        var validator = new ArchitectureValidator();
        bool result = validator.Validate(contractPath, out var violations, out _);

        Assert.That(result, Is.False);
        Assert.That(violations, Has.Some.Matches<ArchitectureViolation>(v => v.SourceType == "allow-forbid-conflict"));
    }

    private string WriteSelfForbiddenPolicy()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Request Overload Test
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
    public void Validate_RequestOverload_SelectsOnlySpecifiedContract()
    {
        string contractPath = WriteSelfForbiddenPolicy();
        var validator = new ArchitectureValidator();

        ValidationOutcome outcome = validator.Validate(new ValidationRequest
        {
            PolicyPath = contractPath,
            Mode = "strict",
            ContractIds = new[] { "harmless" },
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.Violations, Is.Empty);
    }

    [Test]
    public void Validate_RequestOverload_ConditionSet_ResolvesNamedSet()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Condition Set Test
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
");

        var validator = new ArchitectureValidator();

        ValidationOutcome outcome = validator.Validate(new ValidationRequest
        {
            PolicyPath = contractPath,
            Mode = "strict",
            ConditionSetName = "runtime",
        });

        Assert.That(outcome.Passed, Is.True);
    }

    [Test]
    public void Validate_RequestOverload_BaselinePath_SuppressesKnownViolation()
    {
        string contractPath = WriteSelfForbiddenPolicy();
        var validator = new ArchitectureValidator();

        ValidationOutcome before = validator.Validate(new ValidationRequest
        {
            PolicyPath = contractPath,
            Mode = "strict",
            ContractIds = new[] { "self-forbidden" },
        });

        Assert.That(before.Violations, Is.Not.Empty, "Expected at least one baseline violation for test validity");
        ArchitectureViolation known = before.Violations.First();
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

        ValidationOutcome after = validator.Validate(new ValidationRequest
        {
            PolicyPath = contractPath,
            Mode = "strict",
            ContractIds = new[] { "self-forbidden" },
            BaselinePath = baselinePath,
        });

        Assert.That(after.Violations,
            Has.None.Matches<ArchitectureViolation>(v =>
                v.SourceType == known.SourceType && v.ForbiddenReferences.Contains(forbiddenRef)),
            "Baselined violation should be suppressed");
    }

    [Test]
    public void Validate_RequestOverload_EnforcePolicyOff_UnmatchedIgnoreDoesNotFail()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Unmatched Ignore Off Test
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

        var validator = new ArchitectureValidator();

        ValidationOutcome outcome = validator.Validate(new ValidationRequest
        {
            PolicyPath = contractPath,
            Mode = "strict",
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.UnmatchedIgnoredViolations, Is.Empty,
            "Unmatched ignores are not surfaced when EnforceUnmatchedIgnoredViolationsPolicy is off");
    }

    [Test]
    public void Validate_RequestOverload_EnforcePolicyOn_UnmatchedIgnoreFails()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Unmatched Ignore On Test
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

        var validator = new ArchitectureValidator();

        ValidationOutcome outcome = validator.Validate(new ValidationRequest
        {
            PolicyPath = contractPath,
            Mode = "strict",
            EnforceUnmatchedIgnoredViolationsPolicy = true,
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.UnmatchedIgnoredViolations, Is.Not.Empty);
    }

    [Test]
    public void Validate_RequestOverload_WithTimings_PopulatesReport()
    {
        string contractPath = WriteSelfForbiddenPolicy();
        var validator = new ArchitectureValidator();
        var timing = new ValidationTiming();

        validator.Validate(new ValidationRequest
        {
            PolicyPath = contractPath,
            Mode = "strict",
            ContractIds = new[] { "harmless" },
        }, timing);

        using var writer = new StringWriter();
        timing.WriteReport(writer);

        Assert.That(writer.ToString(), Does.Contain("total"));
    }

    [Test]
    public void Validate_LegacyOverloads_UnaffectedByNewOverload()
    {
        string contractPath = WriteSelfForbiddenPolicy();
        var validator = new ArchitectureValidator();

        bool result = validator.Validate(contractPath, out var violations, out var cycles);

        Assert.That(result, Is.False);
        Assert.That(violations, Is.Not.Empty);
        Assert.That(cycles, Is.Empty);
    }
}
