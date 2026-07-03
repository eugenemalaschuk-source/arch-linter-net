using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AssemblyIndependenceValidationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-assembly-independence-test-{Guid.NewGuid():N}");
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

    private string AssemblyName => typeof(AssemblyIndependenceValidationTests).Assembly.GetName().Name!;

    [Test]
    public void AssemblyIndependence_UndeclaredAssembly_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_assembly_independence:
                - name: assembly-independence
                  assemblies: [{AssemblyName}, SomeUndeclaredAssembly]
                  reason: Invalid assembly independence contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("that is not declared in 'analysis.target_assemblies'"));
        Assert.That(ex.Message, Does.Contain("SomeUndeclaredAssembly"));
    }

    [Test]
    public void AssemblyIndependence_AllAssembliesDeclared_LoadsSuccessfully()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}, ArchLinterNet.Core]
            contracts:
              strict_assembly_independence:
                - name: assembly-independence
                  assemblies: [{AssemblyName}, ArchLinterNet.Core]
                  reason: Test assembly independence contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void AssemblyIndependence_DuplicateIds_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}, ArchLinterNet.Core]
            contracts:
              strict_assembly_independence:
                - name: assembly-independence-one
                  id: dup-id
                  assemblies: [{AssemblyName}, ArchLinterNet.Core]
                  reason: First contract.
                - name: assembly-independence-two
                  id: dup-id
                  assemblies: [{AssemblyName}, ArchLinterNet.Core]
                  reason: Second contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("Duplicate contract IDs found"));
    }
}
