using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AssemblyAllowOnlyValidationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-assembly-allow-only-test-{Guid.NewGuid():N}");
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

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private string AssemblyName => typeof(AssemblyAllowOnlyValidationTests).Assembly.GetName().Name!;

    [Test]
    public void AssemblyAllowOnly_UndeclaredSourceAssembly_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_assembly_allow_only:
                - name: assembly-allow-only
                  source: {AssemblyName}
                  allowed: [ArchLinterNet.Core]
                  reason: Invalid assembly allow-only contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("that is not declared in 'analysis.target_assemblies'"));
        Assert.That(ex.Message, Does.Contain(AssemblyName));
    }

    [Test]
    public void AssemblyAllowOnly_UndeclaredAllowedAssembly_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_assembly_allow_only:
                - name: assembly-allow-only
                  source: {AssemblyName}
                  allowed: [SomeUndeclaredAssembly]
                  reason: Invalid assembly allow-only contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("that is not declared in 'analysis.target_assemblies'"));
        Assert.That(ex.Message, Does.Contain("SomeUndeclaredAssembly"));
    }

    [Test]
    public void AssemblyAllowOnly_AllAssembliesDeclared_LoadsSuccessfully()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}, ArchLinterNet.Core]
            contracts:
              strict_assembly_allow_only:
                - name: assembly-allow-only
                  source: {AssemblyName}
                  allowed: [ArchLinterNet.Core]
                  reason: Test assembly allow-only contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void AssemblyAllowOnly_DuplicateIds_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}, ArchLinterNet.Core]
            contracts:
              strict_assembly_allow_only:
                - name: assembly-allow-only-one
                  id: dup-id
                  source: {AssemblyName}
                  allowed: [ArchLinterNet.Core]
                  reason: First contract.
                - name: assembly-allow-only-two
                  id: dup-id
                  source: {AssemblyName}
                  allowed: [ArchLinterNet.Core]
                  reason: Second contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("Duplicate contract IDs found"));
    }
}
