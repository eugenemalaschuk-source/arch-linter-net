using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PackageDependencyValidationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-package-dependency-test-{Guid.NewGuid():N}");
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

    private static string AssemblyName => typeof(PackageDependencyValidationTests).Assembly.GetName().Name!;

    [Test]
    public void PackageDependency_UndeclaredSourceAssembly_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            packages:
              forbidden_infra:
                package_ids: [Microsoft.EntityFrameworkCore]
            contracts:
              strict_package_dependency:
                - name: package-dependency
                  source: {AssemblyName}
                  forbidden: [forbidden_infra]
                  reason: Invalid package dependency contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("that is not declared in 'analysis.target_assemblies'"));
        Assert.That(ex.Message, Does.Contain(AssemblyName));
    }

    [Test]
    public void PackageDependency_DeclaredSourceAssembly_LoadsSuccessfully()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            packages:
              forbidden_infra:
                package_ids: [Microsoft.EntityFrameworkCore]
            contracts:
              strict_package_dependency:
                - name: package-dependency
                  source: {AssemblyName}
                  forbidden: [forbidden_infra]
                  reason: Test package dependency contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void PackageDependency_DependencyDepthTransitive_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            packages:
              forbidden_infra:
                package_ids: [Microsoft.EntityFrameworkCore]
            contracts:
              strict_package_dependency:
                - name: package-dependency
                  source: {AssemblyName}
                  forbidden: [forbidden_infra]
                  dependency_depth: transitive
                  reason: Invalid package dependency contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("dependency_depth: transitive"));
        Assert.That(ex.Message, Does.Contain("not supported yet"));
    }

    [Test]
    public void PackageDependency_DuplicateIds_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            packages:
              forbidden_infra:
                package_ids: [Microsoft.EntityFrameworkCore]
            contracts:
              strict_package_dependency:
                - name: package-dependency-one
                  id: dup-id
                  source: {AssemblyName}
                  forbidden: [forbidden_infra]
                  reason: First contract.
                - name: package-dependency-two
                  id: dup-id
                  source: {AssemblyName}
                  forbidden: [forbidden_infra]
                  reason: Second contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("Duplicate contract IDs found"));
    }

    [Test]
    public void PackageAllowOnly_UndeclaredSourceAssembly_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            packages:
              test_frameworks:
                package_ids: [NUnit]
            contracts:
              strict_package_allow_only:
                - name: package-allow-only
                  source: {AssemblyName}
                  allowed: [test_frameworks]
                  reason: Invalid package allow-only contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("that is not declared in 'analysis.target_assemblies'"));
        Assert.That(ex.Message, Does.Contain(AssemblyName));
    }

    [Test]
    public void PackageAllowOnly_DeclaredSourceAssembly_LoadsSuccessfully()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            packages:
              test_frameworks:
                package_ids: [NUnit]
            contracts:
              strict_package_allow_only:
                - name: package-allow-only
                  source: {AssemblyName}
                  allowed: [test_frameworks]
                  reason: Test package allow-only contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void PackageAllowOnly_DependencyDepthTransitive_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            packages:
              test_frameworks:
                package_ids: [NUnit]
            contracts:
              strict_package_allow_only:
                - name: package-allow-only
                  source: {AssemblyName}
                  allowed: [test_frameworks]
                  dependency_depth: transitive
                  reason: Invalid package allow-only contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("dependency_depth: transitive"));
        Assert.That(ex.Message, Does.Contain("not supported yet"));
    }

    [Test]
    public void PackageAllowOnly_DuplicateIds_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            packages:
              test_frameworks:
                package_ids: [NUnit]
            contracts:
              strict_package_allow_only:
                - name: package-allow-only-one
                  id: dup-id
                  source: {AssemblyName}
                  allowed: [test_frameworks]
                  reason: First contract.
                - name: package-allow-only-two
                  id: dup-id
                  source: {AssemblyName}
                  allowed: [test_frameworks]
                  reason: Second contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("Duplicate contract IDs found"));
    }
}
