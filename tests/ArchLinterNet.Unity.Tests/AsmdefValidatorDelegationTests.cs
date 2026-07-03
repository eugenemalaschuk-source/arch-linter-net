using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Unity.Tests;

[TestFixture]
public sealed class AsmdefValidatorDelegationTests
{
    private string _tempDir = null!;
    private string _policyPath = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-unity-asmdef-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "architecture"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "Assets", "Runtime"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "Assets", "Editor"));

        _policyPath = Path.Combine(_tempDir, "architecture", "dependencies.arch.yml");
        File.WriteAllText(_policyPath, """
            version: 1
            name: Unity Asmdef Test
            contracts:
              strict_asmdef:
                - name: runtime-must-not-reference-editor
                  id: runtime-editor-ref
                  source_assemblies:
                    - Game.Runtime
                  forbidden_editor_refs: true
                  forbidden_asmdef_prefixes: []
            """);

        File.WriteAllText(Path.Combine(_tempDir, "Assets", "Runtime", "Game.Runtime.asmdef"), """
            {
              "name": "Game.Runtime",
              "references": ["Game.Editor"]
            }
            """);

        File.WriteAllText(Path.Combine(_tempDir, "Assets", "Editor", "Game.Editor.asmdef"), """
            {
              "name": "Game.Editor",
              "includePlatforms": ["Editor"]
            }
            """);
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
    public void Validate_WhenStrictAsmdefContractFails_ReturnsCoreViolations()
    {
        var validator = new AsmdefValidator();

        bool passed = validator.Validate(_policyPath, out IReadOnlyCollection<ArchitectureViolation> violations);

        Assert.That(passed, Is.False);
        ArchitectureViolation violation = AssertSingle(violations);
        Assert.That(violation.ContractName, Is.EqualTo("runtime-must-not-reference-editor"));
        Assert.That(violation.ContractId, Is.EqualTo("runtime-editor-ref"));
        Assert.That(violation.SourceType, Is.EqualTo("Game.Runtime"));
        Assert.That(violation.ForbiddenNamespace, Is.EqualTo("asmdef-references"));
        Assert.That(violation.ForbiddenReferences, Is.EquivalentTo(new[] { "Game.Editor" }));
    }

    private static ArchitectureViolation AssertSingle(IReadOnlyCollection<ArchitectureViolation> violations)
    {
        Assert.That(violations, Has.Count.EqualTo(1));
        return violations.Single();
    }
}
