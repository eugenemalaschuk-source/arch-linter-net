using ArchLinterNet.Core;
using ArchLinterNet.Core.Model;
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
}
