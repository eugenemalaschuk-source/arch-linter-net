using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class FrameworkReferenceValidationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-framework-reference-test-{Guid.NewGuid():N}");
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

    private static string AssemblyName => typeof(FrameworkReferenceValidationTests).Assembly.GetName().Name!;

    [Test]
    public void FrameworkDependency_UndeclaredSourceAssembly_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            framework_references:
              forbidden_web:
                framework_names: [Microsoft.AspNetCore.App]
            contracts:
              strict_framework_dependency:
                - name: framework-dependency
                  source: {AssemblyName}
                  forbidden: [forbidden_web]
                  reason: Invalid framework dependency contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("that is not declared in 'analysis.target_assemblies'"));
        Assert.That(ex.Message, Does.Contain(AssemblyName));
    }

    [Test]
    public void FrameworkDependency_DeclaredSourceAssembly_LoadsSuccessfully()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            framework_references:
              forbidden_web:
                framework_names: [Microsoft.AspNetCore.App]
            contracts:
              strict_framework_dependency:
                - name: framework-dependency
                  source: {AssemblyName}
                  forbidden: [forbidden_web]
                  reason: Test framework dependency contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void FrameworkDependency_DuplicateIds_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            framework_references:
              forbidden_web:
                framework_names: [Microsoft.AspNetCore.App]
            contracts:
              strict_framework_dependency:
                - name: framework-dependency-one
                  id: dup-id
                  source: {AssemblyName}
                  forbidden: [forbidden_web]
                  reason: First contract.
                - name: framework-dependency-two
                  id: dup-id
                  source: {AssemblyName}
                  forbidden: [forbidden_web]
                  reason: Second contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("Duplicate contract IDs found"));
    }

    [Test]
    public void FrameworkAllowOnly_UndeclaredSourceAssembly_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            framework_references:
              core:
                framework_names: [Microsoft.NETCore.App]
            contracts:
              strict_framework_allow_only:
                - name: framework-allow-only
                  source: {AssemblyName}
                  allowed: [core]
                  reason: Invalid framework allow-only contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("that is not declared in 'analysis.target_assemblies'"));
        Assert.That(ex.Message, Does.Contain(AssemblyName));
    }

    [Test]
    public void FrameworkAllowOnly_DeclaredSourceAssembly_LoadsSuccessfully()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            framework_references:
              core:
                framework_names: [Microsoft.NETCore.App]
            contracts:
              strict_framework_allow_only:
                - name: framework-allow-only
                  source: {AssemblyName}
                  allowed: [core]
                  reason: Test framework allow-only contract.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(policyPath));
    }

    [Test]
    public void FrameworkAllowOnly_DuplicateIds_ThrowsActionableError()
    {
        string policyPath = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            framework_references:
              core:
                framework_names: [Microsoft.NETCore.App]
            contracts:
              strict_framework_allow_only:
                - name: framework-allow-only-one
                  id: dup-id
                  source: {AssemblyName}
                  allowed: [core]
                  reason: First contract.
                - name: framework-allow-only-two
                  id: dup-id
                  source: {AssemblyName}
                  allowed: [core]
                  reason: Second contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("Duplicate contract IDs found"));
    }
}
