using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AssemblySourceSetExpansionTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-assembly-source-sets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void DirectionalAssemblyContracts_ExpandLargeSourceSetWithDistinctDerivedIds()
    {
        string[] modules = Enumerable.Range(1, 24).Select(index => $"Acme.Modules.M{index:D2}").ToArray();
        string targetAssemblies = string.Join(", ", modules.Append("Acme.Shared.Abstractions"));
        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(WritePolicy($"""
            version: 1
            name: Directional assembly source expansion
            analysis:
              target_assemblies: [{targetAssemblies}]
            source_sets:
              modules:
                kind: assembly
                globs: ["Acme.Modules.*"]
            contracts:
              strict_assembly_dependency:
                - name: modules avoid forbidden assemblies
                  id: modules-no-forbidden
                  source_sets: [modules]
                  forbidden: [Acme.Shared.Abstractions]
              audit_assembly_allow_only:
                - name: modules use shared abstractions only
                  id: modules-allow-only
                  source_sets: [modules]
                  allowed: [Acme.Shared.Abstractions]
            """));

        Assert.Multiple(() =>
        {
            Assert.That(document.Contracts.StrictAssemblyDependency.Select(contract => contract.Source),
                Is.EqualTo(modules));
            Assert.That(document.Contracts.AuditAssemblyAllowOnly.Select(contract => contract.Id),
                Is.EqualTo(modules.Select(module => $"modules-allow-only/{module.ToLowerInvariant().Replace('.', '-')}")));
            Assert.That(document.SourceExpansion.Contracts.Select(expansion => expansion.AuthoredContractId),
                Is.EqualTo(new[] { "modules-no-forbidden", "modules-allow-only" }));
        });
    }

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_temporaryDirectory, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }
}
