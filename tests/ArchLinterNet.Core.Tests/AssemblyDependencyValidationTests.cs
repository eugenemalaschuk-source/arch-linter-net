using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AssemblyDependencyValidationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-assembly-dependency-test-{Guid.NewGuid():N}");
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

    private static string AssemblyName => typeof(AssemblyDependencyValidationTests).Assembly.GetName().Name!;

    [Test]
    public void AssemblyDependency_UndeclaredSourceAssembly_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_assembly_dependency:
                - name: assembly-dependency
                  source: {AssemblyName}
                  forbidden: [ArchLinterNet.Core]
                  reason: Invalid assembly dependency contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("that is not declared in 'analysis.target_assemblies'"));
        Assert.That(ex.Message, Does.Contain(AssemblyName));
    }

    [Test]
    public void AssemblyDependency_UndeclaredForbiddenAssembly_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_assembly_dependency:
                - name: assembly-dependency
                  source: {AssemblyName}
                  forbidden: [SomeUndeclaredAssembly]
                  reason: Invalid assembly dependency contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("that is not declared in 'analysis.target_assemblies'"));
        Assert.That(ex.Message, Does.Contain("SomeUndeclaredAssembly"));
    }

    [Test]
    public void AssemblyDependency_AllAssembliesDeclared_LoadsSuccessfully()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}, ArchLinterNet.Core]
            contracts:
              strict_assembly_dependency:
                - name: assembly-dependency
                  source: {AssemblyName}
                  forbidden: [ArchLinterNet.Core]
                  reason: Test assembly dependency contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void AssemblyDependency_ExplicitDependencyDepthDirect_LoadsSuccessfully()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}, ArchLinterNet.Core]
            contracts:
              strict_assembly_dependency:
                - name: assembly-dependency
                  source: {AssemblyName}
                  forbidden: [ArchLinterNet.Core]
                  dependency_depth: direct
                  reason: Test assembly dependency contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void AssemblyDependency_DependencyDepthTransitive_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}, ArchLinterNet.Core]
            contracts:
              strict_assembly_dependency:
                - name: assembly-dependency
                  source: {AssemblyName}
                  forbidden: [ArchLinterNet.Core]
                  dependency_depth: transitive
                  reason: Invalid assembly dependency contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("dependency_depth: transitive"));
        Assert.That(ex.Message, Does.Contain("not supported yet"));
    }

    [Test]
    public void AssemblyDependency_DuplicateIds_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}, ArchLinterNet.Core]
            contracts:
              strict_assembly_dependency:
                - name: assembly-dependency-one
                  id: dup-id
                  source: {AssemblyName}
                  forbidden: [ArchLinterNet.Core]
                  reason: First contract.
                - name: assembly-dependency-two
                  id: dup-id
                  source: {AssemblyName}
                  forbidden: [ArchLinterNet.Core]
                  reason: Second contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("Duplicate contract IDs found"));
    }
}
