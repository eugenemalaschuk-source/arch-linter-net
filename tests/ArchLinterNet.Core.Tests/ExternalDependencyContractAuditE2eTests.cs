using ArchLinterNet.Core.Model;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// E2E: runs the real audit pipeline (full IL-token resolution and reference scanning) against
// the actual ArchLinterNet.Core assembly — tens of seconds on CI runners. Kept out of the unit
// path via Category E2E and capped with [CancelAfter]; the perf follow-up is tracked in
// https://github.com/eugenemalaschuk-source/arch-linter-net/issues/419.
[TestFixture]
[Category("E2E")]
public sealed class ExternalDependencyContractAuditE2eTests
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
    [CancelAfter(180_000)]
    public void ValidateAudit_AuditExternalViolation_IsReported()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Audit External Test
layers:
  core:
    namespace: ArchLinterNet.Core
external_dependencies:
  system:
    namespace_prefixes:
      - System
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
  strict_protected: []
  strict_external: []
  audit_external:
    - name: core-audit-system
      source: core
      forbidden: [system]
");

        var result = ArchitectureAssertions.FromPolicy(contractPath).ValidateAudit();

        Assert.That(result.Passed, Is.False);
        Assert.That(result.Violations.Any(v => (v.Payload as ExternalDependencyPayload)?.ForbiddenExternalGroup == "system"), Is.True);
    }
}
